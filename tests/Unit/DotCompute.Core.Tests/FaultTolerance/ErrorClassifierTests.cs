// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Net.Sockets;
using DotCompute.Abstractions.FaultTolerance;
using DotCompute.Core.FaultTolerance;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DotCompute.Core.Tests.FaultTolerance;

/// <summary>
/// Unit tests for <see cref="ErrorClassifier"/>.
/// </summary>
[Trait("Category", "Unit")]
public class ErrorClassifierTests
{
    private readonly ILogger<ErrorClassifier> _logger;

    public ErrorClassifierTests()
    {
        _logger = Substitute.For<ILogger<ErrorClassifier>>();
    }

    private ErrorClassifier CreateClassifier(ErrorClassifierOptions? options = null) =>
        new(_logger, options);

    #region Classification Tests

    [Fact]
    public void Classify_TimeoutException_ReturnsTimeout()
    {
        // Arrange
        var classifier = CreateClassifier();
        var exception = new TimeoutException("Operation timed out");

        // Act
        var result = classifier.Classify(exception);

        // Assert
        Assert.Equal(ErrorCategory.Timeout, result.Category);
        Assert.True(result.IsRetryable);
        Assert.Equal(RecoveryAction.RetryWithBackoff, result.RecommendedAction);
    }

    [Fact]
    public void Classify_OperationCanceledException_ReturnsCancellation()
    {
        // Arrange
        var classifier = CreateClassifier();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var exception = new OperationCanceledException(cts.Token);

        // Act
        var result = classifier.Classify(exception);

        // Assert
        Assert.Equal(ErrorCategory.Transient, result.Category);
        Assert.False(result.IsRetryable);
        Assert.Equal(RecoveryAction.Skip, result.RecommendedAction);
    }

    [Fact]
    public void Classify_OutOfMemoryException_ReturnsResourceExhaustion()
    {
        // Arrange
        var classifier = CreateClassifier();
#pragma warning disable CA2201 // Test requires creating reserved exception type
        var exception = new OutOfMemoryException("Not enough memory");
#pragma warning restore CA2201

        // Act
        var result = classifier.Classify(exception);

        // Assert
        Assert.Equal(ErrorCategory.ResourceExhaustion, result.Category);
        Assert.Equal(ErrorSeverity.Critical, result.Severity);
        Assert.True(result.IsRetryable);
    }

    [Fact]
    public void Classify_SocketException_ConnectionRefused_ReturnsTransient()
    {
        // Arrange
        var classifier = CreateClassifier();
        var exception = new SocketException((int)SocketError.ConnectionRefused);

        // Act
        var result = classifier.Classify(exception);

        // Assert
        Assert.Equal(ErrorCategory.Transient, result.Category);
        Assert.True(result.IsRetryable);
    }

    [Fact]
    public void Classify_SocketException_TimedOut_ReturnsTimeout()
    {
        // Arrange
        var classifier = CreateClassifier();
        var exception = new SocketException((int)SocketError.TimedOut);

        // Act
        var result = classifier.Classify(exception);

        // Assert
        Assert.Equal(ErrorCategory.Timeout, result.Category);
        Assert.True(result.IsRetryable);
    }

    [Fact]
    public void Classify_SocketException_TooManyOpenSockets_ReturnsResourceExhaustion()
    {
        // Arrange
        var classifier = CreateClassifier();
        var exception = new SocketException((int)SocketError.TooManyOpenSockets);

        // Act
        var result = classifier.Classify(exception);

        // Assert
        Assert.Equal(ErrorCategory.ResourceExhaustion, result.Category);
    }

    [Fact]
    public void Classify_ArgumentException_ReturnsPermanent()
    {
        // Arrange
        var classifier = CreateClassifier();
        var exception = new ArgumentException("Invalid argument");

        // Act
        var result = classifier.Classify(exception);

        // Assert
        Assert.Equal(ErrorCategory.Permanent, result.Category);
        Assert.False(result.IsRetryable);
        Assert.Equal(RecoveryAction.FailFast, result.RecommendedAction);
    }

    [Fact]
    public void Classify_InvalidOperationException_ReturnsPermanent()
    {
        // Arrange
        var classifier = CreateClassifier();
        var exception = new InvalidOperationException("Invalid state");

        // Act
        var result = classifier.Classify(exception);

        // Assert
        Assert.Equal(ErrorCategory.Permanent, result.Category);
        Assert.False(result.IsRetryable);
    }

    [Fact]
    public void Classify_UnauthorizedAccessException_ReturnsSecurity()
    {
        // Arrange
        var classifier = CreateClassifier();
        var exception = new UnauthorizedAccessException("Access denied");

        // Act
        var result = classifier.Classify(exception);

        // Assert
        Assert.Equal(ErrorCategory.Security, result.Category);
        Assert.False(result.IsRetryable);
        Assert.Equal(RecoveryAction.Reconfigure, result.RecommendedAction);
    }

    [Fact]
    public void Classify_FormatException_ReturnsDataIntegrity()
    {
        // Arrange
        var classifier = CreateClassifier();
        var exception = new FormatException("Invalid format");

        // Act
        var result = classifier.Classify(exception);

        // Assert
        Assert.Equal(ErrorCategory.DataIntegrity, result.Category);
        Assert.False(result.IsRetryable);
    }

    [Fact]
    public void Classify_ObjectDisposedException_ReturnsPermanent()
    {
        // Arrange
        var classifier = CreateClassifier();
        var exception = new ObjectDisposedException("TestObject");

        // Act
        var result = classifier.Classify(exception);

        // Assert
        Assert.Equal(ErrorCategory.Permanent, result.Category);
        Assert.Equal(RecoveryAction.RestartComponent, result.RecommendedAction);
    }

    [Fact]
    public void Classify_SynchronizationLockException_ReturnsConcurrency()
    {
        // Arrange
        var classifier = CreateClassifier();
        var exception = new SynchronizationLockException("Lock failed");

        // Act
        var result = classifier.Classify(exception);

        // Assert
        Assert.Equal(ErrorCategory.Concurrency, result.Category);
        Assert.True(result.IsRetryable);
    }

    [Fact]
    public void Classify_HttpRequestException_ReturnsTransient()
    {
        // Arrange
        var classifier = CreateClassifier();
        var exception = new HttpRequestException("Request failed");

        // Act
        var result = classifier.Classify(exception);

        // Assert
        Assert.Equal(ErrorCategory.Transient, result.Category);
        Assert.True(result.IsRetryable);
    }

    [Fact]
    public void Classify_IOException_DiskFull_ReturnsResourceExhaustion()
    {
        // Arrange
        var classifier = CreateClassifier();
        var exception = new IOException("Disk full - no space left on device");

        // Act
        var result = classifier.Classify(exception);

        // Assert
        Assert.Equal(ErrorCategory.ResourceExhaustion, result.Category);
    }

    [Fact]
    public void Classify_IOException_FileLocked_ReturnsConcurrency()
    {
        // Arrange
        var classifier = CreateClassifier();
        var exception = new IOException("The file is being used by another process (locked)");

        // Act
        var result = classifier.Classify(exception);

        // Assert
        Assert.Equal(ErrorCategory.Concurrency, result.Category);
    }

    [Fact]
    public void Classify_IOException_FileNotFound_ReturnsPermanent()
    {
        // Arrange
        var classifier = CreateClassifier();
        var exception = new IOException("File not found");

        // Act
        var result = classifier.Classify(exception);

        // Assert
        Assert.Equal(ErrorCategory.Permanent, result.Category);
        Assert.False(result.IsRetryable);
    }

    [Fact]
    public void Classify_AggregateException_AllTransient_ReturnsRetryable()
    {
        // Arrange
        var classifier = CreateClassifier();
        // Using exceptions that are all classified as retryable (transient or timeout)
        var exception = new AggregateException(
            new HttpRequestException("Network error 1"),
            new HttpRequestException("Network error 2"));

        // Act
        var result = classifier.Classify(exception);

        // Assert - all inner exceptions are transient (HttpRequestException), so the aggregate should be transient
        Assert.Equal(ErrorCategory.Transient, result.Category);
        Assert.True(result.IsRetryable);
    }

    [Fact]
    public void Classify_AggregateException_AnyPermanent_ReturnsPermanent()
    {
        // Arrange
        var classifier = CreateClassifier();
        var exception = new AggregateException(
            new TimeoutException("Timeout"),
            new ArgumentException("Invalid argument"));

        // Act
        var result = classifier.Classify(exception);

        // Assert
        Assert.Equal(ErrorCategory.Permanent, result.Category);
        Assert.False(result.IsRetryable);
    }

    [Fact]
    public void Classify_UnknownException_ReturnsUnknown()
    {
        // Arrange
        var classifier = CreateClassifier();
        var exception = new CustomTestException("Unknown error type");

        // Act
        var result = classifier.Classify(exception);

        // Assert
        Assert.Equal(ErrorCategory.Unknown, result.Category);
    }

    [Fact]
    public void Classify_ExceptionWithTimeoutInMessage_ReturnsTimeout()
    {
        // Arrange
        var classifier = CreateClassifier();
        var exception = new CustomTestException("The operation timed out");

        // Act
        var result = classifier.Classify(exception);

        // Assert
        Assert.Equal(ErrorCategory.Timeout, result.Category);
    }

    [Fact]
    public void Classify_ExceptionWithCudaInMessage_ReturnsGpuError()
    {
        // Arrange
        var classifier = CreateClassifier();
        var exception = new CustomTestException("CUDA error: out of memory");

        // Act
        var result = classifier.Classify(exception);

        // Assert
        Assert.Equal(ErrorCategory.GpuError, result.Category);
        Assert.True(result.IsRetryable);
        Assert.Equal(RecoveryAction.Failover, result.RecommendedAction);
    }

    #endregion

    #region Custom Classifier Tests

    [Fact]
    public void Classify_WithCustomClassifier_UsesCustomClassifierFirst()
    {
        // Arrange
        var options = new ErrorClassifierOptions
        {
            CustomClassifiers =
            [
                ex => ex is CustomTestException
                    ? new ErrorClassification
                    {
                        Category = ErrorCategory.Configuration,
                        Severity = ErrorSeverity.Warning,
                        RecommendedAction = RecoveryAction.Reconfigure,
                        IsRetryable = false,
                        Description = "Custom classification"
                    }
                    : null
            ]
        };

        var classifier = CreateClassifier(options);
        var exception = new CustomTestException("Test error");

        // Act
        var result = classifier.Classify(exception);

        // Assert
        Assert.Equal(ErrorCategory.Configuration, result.Category);
        Assert.Equal("Custom classification", result.Description);
    }

    [Fact]
    public void Classify_CustomClassifierReturnsNull_FallsBackToBuiltIn()
    {
        // Arrange
        var options = new ErrorClassifierOptions
        {
            CustomClassifiers = [ex => null]
        };

        var classifier = CreateClassifier(options);
        var exception = new TimeoutException("Timeout");

        // Act
        var result = classifier.Classify(exception);

        // Assert
        Assert.Equal(ErrorCategory.Timeout, result.Category);
    }

    #endregion

    #region Diagnostics Tests

    [Fact]
    public void CreateDiagnostics_ReturnsDetailedInfo()
    {
        // Arrange
        var classifier = CreateClassifier();
        var exception = new InvalidOperationException("Test error");

        // Act
        var diagnostics = classifier.CreateDiagnostics(
            exception,
            "test-component",
            "TestOperation",
            "corr-123");

        // Assert
        Assert.NotNull(diagnostics);
        Assert.Equal(exception, diagnostics.Exception);
        Assert.Equal("test-component", diagnostics.ComponentId);
        Assert.Equal("TestOperation", diagnostics.Operation);
        Assert.Equal("corr-123", diagnostics.CorrelationId);
        Assert.NotNull(diagnostics.Classification);
        Assert.Equal(ErrorCategory.Permanent, diagnostics.Classification.Category);
    }

    [Fact]
    public void CreateDiagnostics_IncludesStackFrames()
    {
        // Arrange
        var classifier = CreateClassifier();
        Exception exception;

        try
        {
            throw new InvalidOperationException("Test");
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        // Act
        var diagnostics = classifier.CreateDiagnostics(exception);

        // Assert
        Assert.NotEmpty(diagnostics.StackFrames);
        Assert.Contains(diagnostics.StackFrames, f =>
            f.MethodName == nameof(CreateDiagnostics_IncludesStackFrames));
    }

    [Fact]
    public void CreateDiagnostics_IncludesEnvironmentSnapshot()
    {
        // Arrange
        var classifier = CreateClassifier();
#pragma warning disable CA2201 // Test intentionally uses generic Exception
        var exception = new Exception("Test");
#pragma warning restore CA2201

        // Act
        var diagnostics = classifier.CreateDiagnostics(exception);

        // Assert
        Assert.NotNull(diagnostics.Environment);
        Assert.NotNull(diagnostics.Environment.MachineName);
        Assert.True(diagnostics.Environment.ProcessId > 0);
        Assert.True(diagnostics.Environment.ProcessorCount > 0);
    }

    [Fact]
    public void CreateDiagnostics_WithoutEnvironmentSnapshot_WhenDisabled()
    {
        // Arrange
        var options = new ErrorClassifierOptions { IncludeEnvironmentSnapshot = false };
        var classifier = CreateClassifier(options);
#pragma warning disable CA2201 // Test intentionally uses generic Exception
        var exception = new Exception("Test");
#pragma warning restore CA2201

        // Act
        var diagnostics = classifier.CreateDiagnostics(exception);

        // Assert
        Assert.Null(diagnostics.Environment);
    }

    [Fact]
    public void CreateDiagnostics_IncludesInnerExceptions()
    {
        // Arrange
        var classifier = CreateClassifier();
        var inner = new ArgumentException("Inner error");
        var exception = new InvalidOperationException("Outer error", inner);

        // Act
        var diagnostics = classifier.CreateDiagnostics(exception);

        // Assert
        Assert.Single(diagnostics.InnerErrors);
        Assert.Equal(inner, diagnostics.InnerErrors[0].Exception);
    }

    [Fact]
    public void CreateDiagnostics_IncludesExceptionData()
    {
        // Arrange
        var classifier = CreateClassifier();
#pragma warning disable CA2201 // Test intentionally uses generic Exception to access Data property
        var exception = new Exception("Test");
#pragma warning restore CA2201
        exception.Data["Key1"] = "Value1";
        exception.Data["Key2"] = 42;

        // Act
        var diagnostics = classifier.CreateDiagnostics(exception);

        // Assert
        Assert.NotNull(diagnostics.Context);
        Assert.Equal("Value1", diagnostics.Context["Key1"]);
        Assert.Equal(42, diagnostics.Context["Key2"]);
    }

    #endregion

    #region IsRetryable Tests

    [Fact]
    public void IsRetryable_TransientError_ReturnsTrue()
    {
        // Arrange
        var classifier = CreateClassifier();

        // Act & Assert
        Assert.True(classifier.IsRetryable(new TimeoutException()));
        Assert.True(classifier.IsRetryable(new HttpRequestException()));
        Assert.True(classifier.IsRetryable(new SocketException((int)SocketError.ConnectionRefused)));
    }

    [Fact]
    public void IsRetryable_PermanentError_ReturnsFalse()
    {
        // Arrange
        var classifier = CreateClassifier();

        // Act & Assert
        Assert.False(classifier.IsRetryable(new ArgumentException()));
        Assert.False(classifier.IsRetryable(new InvalidOperationException()));
        Assert.False(classifier.IsRetryable(new NotSupportedException()));
    }

    #endregion

    #region Retry Delay Tests

    [Fact]
    public void GetRetryDelay_ReturnsExponentialBackoff()
    {
        // Arrange
        var classifier = CreateClassifier();
        var exception = new TimeoutException();

        // Act
        var delay1 = classifier.GetRetryDelay(exception, 1);
        var delay2 = classifier.GetRetryDelay(exception, 2);
        var delay3 = classifier.GetRetryDelay(exception, 3);

        // Assert
        Assert.True(delay2 > delay1);
        Assert.True(delay3 > delay2);
    }

    [Fact]
    public void GetRetryDelay_NonRetryable_ReturnsZero()
    {
        // Arrange
        var classifier = CreateClassifier();
        var exception = new ArgumentException();

        // Act
        var delay = classifier.GetRetryDelay(exception, 1);

        // Assert
        Assert.Equal(TimeSpan.Zero, delay);
    }

    [Fact]
    public void GetRetryDelay_RespectsMaxDelay()
    {
        // Arrange
        var options = new ErrorClassifierOptions
        {
            MaxRetryDelay = TimeSpan.FromSeconds(5)
        };
        var classifier = CreateClassifier(options);
        var exception = new TimeoutException();

        // Act
        var delay = classifier.GetRetryDelay(exception, 100); // Very high attempt

        // Assert
        Assert.True(delay <= TimeSpan.FromSeconds(5));
    }

    #endregion

    #region Extension Method Tests

    [Fact]
    public void ShouldRetry_WithinMaxAttempts_ReturnsTrue()
    {
        // Arrange
        var classification = ErrorClassification.Transient("Test", maxRetries: 3);

        // Act & Assert
        Assert.True(classification.ShouldRetry(1));
        Assert.True(classification.ShouldRetry(2));
        Assert.False(classification.ShouldRetry(3));
        Assert.False(classification.ShouldRetry(4));
    }

    [Fact]
    public void ShouldRetry_NonRetryable_ReturnsFalse()
    {
        // Arrange
        var classification = ErrorClassification.Permanent("Test");

        // Act & Assert
        Assert.False(classification.ShouldRetry(1));
    }

    [Fact]
    public void GetExponentialBackoff_CalculatesCorrectly()
    {
        // Arrange
        var classification = new ErrorClassification
        {
            IsRetryable = true,
            RecommendedRetryDelay = TimeSpan.FromSeconds(1)
        };

        // Act
        var delay1 = classification.GetExponentialBackoff(1);
        var delay2 = classification.GetExponentialBackoff(2);
        var delay3 = classification.GetExponentialBackoff(3);

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(1), delay1);
        Assert.Equal(TimeSpan.FromSeconds(2), delay2);
        Assert.Equal(TimeSpan.FromSeconds(4), delay3);
    }

    [Fact]
    public void GetExponentialBackoff_RespectsMaxDelay()
    {
        // Arrange
        var classification = new ErrorClassification
        {
            IsRetryable = true,
            RecommendedRetryDelay = TimeSpan.FromSeconds(1)
        };

        // Act
        var delay = classification.GetExponentialBackoff(
            10,
            maxDelay: TimeSpan.FromSeconds(30));

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(30), delay);
    }

    [Fact]
    public void ClassifyAndDiagnose_CombinesClassificationAndDiagnostics()
    {
        // Arrange
        var classifier = CreateClassifier();
        var exception = new TimeoutException("Test timeout");

        // Act
        var diagnostics = classifier.ClassifyAndDiagnose(
            exception,
            "component-1",
            "Operation");

        // Assert
        Assert.NotNull(diagnostics);
        Assert.Equal(ErrorCategory.Timeout, diagnostics.Classification.Category);
        Assert.Equal("component-1", diagnostics.ComponentId);
    }

    #endregion

    #region Static Factory Tests

    [Fact]
    public void ErrorClassification_Unknown_CreatesCorrectly()
    {
        // Act
        var classification = ErrorClassification.Unknown("Unknown error");

        // Assert
        Assert.Equal(ErrorCategory.Unknown, classification.Category);
        Assert.Equal(ErrorSeverity.Error, classification.Severity);
        Assert.False(classification.IsRetryable);
    }

    [Fact]
    public void ErrorClassification_Transient_CreatesCorrectly()
    {
        // Act
        var classification = ErrorClassification.Transient(
            "Transient error",
            TimeSpan.FromSeconds(2),
            5);

        // Assert
        Assert.Equal(ErrorCategory.Transient, classification.Category);
        Assert.Equal(ErrorSeverity.Warning, classification.Severity);
        Assert.True(classification.IsRetryable);
        Assert.Equal(TimeSpan.FromSeconds(2), classification.RecommendedRetryDelay);
        Assert.Equal(5, classification.MaxRetryAttempts);
    }

    [Fact]
    public void ErrorClassification_Permanent_CreatesCorrectly()
    {
        // Act
        var classification = ErrorClassification.Permanent(
            "Permanent error",
            RecoveryAction.Fallback);

        // Assert
        Assert.Equal(ErrorCategory.Permanent, classification.Category);
        Assert.Equal(ErrorSeverity.Error, classification.Severity);
        Assert.False(classification.IsRetryable);
        Assert.Equal(RecoveryAction.Fallback, classification.RecommendedAction);
    }

    #endregion

#pragma warning disable CA1032, CA1064 // Test-only exception class
    private sealed class CustomTestException : Exception
    {
        public CustomTestException(string message) : base(message) { }
    }
#pragma warning restore CA1032, CA1064
}
