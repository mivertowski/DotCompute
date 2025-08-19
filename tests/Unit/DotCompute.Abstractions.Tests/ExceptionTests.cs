// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Xunit;
using FluentAssertions;
using System.Globalization;

namespace DotCompute.Abstractions.Tests;


/// <summary>
/// Comprehensive unit tests for AcceleratorException and MemoryException classes.
/// </summary>
public sealed class ExceptionTests
{
    #region AcceleratorException Tests

    [Fact]
    public void AcceleratorException_DefaultConstructor_ShouldCreateExceptionWithDefaultMessage()
    {
        // Act
        var exception = new AcceleratorException();

        // Assert
        Assert.NotNull(exception);
        _ = exception.Message.Should().NotBeNullOrEmpty();
        _ = exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void AcceleratorException_MessageConstructor_ShouldCreateExceptionWithMessage()
    {
        // Arrange
        const string expectedMessage = "Accelerator operation failed";

        // Act
        var exception = new AcceleratorException(expectedMessage);

        // Assert
        _ = exception.Message.Should().Be(expectedMessage);
        _ = exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void AcceleratorException_MessageAndInnerExceptionConstructor_ShouldCreateExceptionWithBoth()
    {
        // Arrange
        const string expectedMessage = "Accelerator operation failed";
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new AcceleratorException(expectedMessage, innerException);

        // Assert
        _ = exception.Message.Should().Be(expectedMessage);
        _ = exception.InnerException.Should().Be(innerException);
    }

    [Fact]
    public void AcceleratorException_WithNullMessage_ShouldAcceptNull()
    {
        // Act
        var exception = new AcceleratorException(null!);

        // Assert
        Assert.NotNull(exception);
        // Message behavior with null is framework-dependent
    }

    [Fact]
    public void AcceleratorException_WithEmptyMessage_ShouldAcceptEmptyString()
    {
        // Act
        var exception = new AcceleratorException(string.Empty);

        // Assert
        Assert.NotNull(exception);
        _ = exception.Message.Should().Be(string.Empty);
    }

    [Fact]
    public void AcceleratorException_WithNullInnerException_ShouldAcceptNull()
    {
        // Arrange
        const string message = "Test message";

        // Act
        var exception = new AcceleratorException(message, null!);

        // Assert
        _ = exception.Message.Should().Be(message);
        _ = exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void AcceleratorException_ShouldInheritFromException()
    {
        // Arrange & Act
        var exception = new AcceleratorException();

        // Assert
        _ = Assert.IsAssignableFrom<Exception>(exception);
    }

    [Theory]
    [InlineData("CUDA initialization failed")]
    [InlineData("Device memory allocation failed")]
    [InlineData("Kernel compilation error: syntax error at line 42")]
    [InlineData("OpenCL runtime error: CL_INVALID_KERNEL")]
    public void AcceleratorException_WithVariousMessages_ShouldPreserveMessage(string message)
    {
        // Act
        var exception = new AcceleratorException(message);

        // Assert
        _ = exception.Message.Should().Be(message);
    }

    [Fact]
    public void AcceleratorException_WithNestedInnerExceptions_ShouldPreserveHierarchy()
    {
        // Arrange
        var rootException = new InvalidOperationException("Root cause");
        var middleException = new ArgumentException("Middle layer", rootException);
        var topException = new AcceleratorException("Top level error", middleException);

        // Act & Assert
        _ = topException.InnerException.Should().Be(middleException);
        _ = topException.InnerException!.InnerException.Should().Be(rootException);
        _ = topException.InnerException.InnerException!.InnerException.Should().BeNull();
    }

    #endregion

    #region MemoryException Tests

    [Fact]
    public void MemoryException_DefaultConstructor_ShouldCreateExceptionWithDefaultMessage()
    {
        // Act
        var exception = new MemoryException();

        // Assert
        Assert.NotNull(exception);
        _ = exception.Message.Should().NotBeNullOrEmpty();
        _ = exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void MemoryException_MessageConstructor_ShouldCreateExceptionWithMessage()
    {
        // Arrange
        const string expectedMessage = "Memory allocation failed";

        // Act
        var exception = new MemoryException(expectedMessage);

        // Assert
        _ = exception.Message.Should().Be(expectedMessage);
        _ = exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void MemoryException_MessageAndInnerExceptionConstructor_ShouldCreateExceptionWithBoth()
    {
        // Arrange
        const string expectedMessage = "Memory allocation failed";
        var innerException = new InvalidOperationException("System memory issue");

        // Act
        var exception = new MemoryException(expectedMessage, innerException);

        // Assert
        _ = exception.Message.Should().Be(expectedMessage);
        _ = exception.InnerException.Should().Be(innerException);
    }

    [Fact]
    public void MemoryException_WithNullMessage_ShouldAcceptNull()
    {
        // Act
        var exception = new MemoryException(null!);

        // Assert
        Assert.NotNull(exception);
        // Message behavior with null is framework-dependent
    }

    [Fact]
    public void MemoryException_WithEmptyMessage_ShouldAcceptEmptyString()
    {
        // Act
        var exception = new MemoryException(string.Empty);

        // Assert
        Assert.NotNull(exception);
        _ = exception.Message.Should().Be(string.Empty);
    }

    [Fact]
    public void MemoryException_WithNullInnerException_ShouldAcceptNull()
    {
        // Arrange
        const string message = "Test message";

        // Act
        var exception = new MemoryException(message, null!);

        // Assert
        _ = exception.Message.Should().Be(message);
        _ = exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void MemoryException_ShouldInheritFromException()
    {
        // Arrange & Act
        var exception = new MemoryException();

        // Assert
        _ = Assert.IsAssignableFrom<Exception>(exception);
    }

    [Theory]
    [InlineData("Out of GPU memory")]
    [InlineData("Buffer allocation failed: insufficient device memory")]
    [InlineData("Memory copy operation failed")]
    [InlineData("Invalid memory address: 0x0000000000000000")]
    public void MemoryException_WithVariousMessages_ShouldPreserveMessage(string message)
    {
        // Act
        var exception = new MemoryException(message);

        // Assert
        _ = exception.Message.Should().Be(message);
    }

    [Fact]
    public void MemoryException_WithNestedInnerExceptions_ShouldPreserveHierarchy()
    {
        // Arrange
        var rootException = new InvalidOperationException("System memory exhausted");
        var middleException = new InvalidOperationException("Memory pool allocation failed", rootException);
        var topException = new MemoryException("Device memory allocation failed", middleException);

        // Act & Assert
        _ = topException.InnerException.Should().Be(middleException);
        _ = topException.InnerException!.InnerException.Should().Be(rootException);
        _ = topException.InnerException.InnerException!.InnerException.Should().BeNull();
    }

    #endregion

    #region Exception Hierarchy Tests

    [Fact]
    public void AcceleratorException_AndMemoryException_ShouldBothInheritFromException()
    {
        // Arrange & Act
        var acceleratorException = new AcceleratorException();
        var memoryException = new MemoryException();

        // Assert
        _ = Assert.IsAssignableFrom<Exception>(acceleratorException);
        _ = Assert.IsAssignableFrom<Exception>(memoryException);
    }

    [Fact]
    public void AcceleratorException_AndMemoryException_ShouldNotInheritFromEachOther()
    {
        // Arrange & Act
        var acceleratorException = new AcceleratorException();
        var memoryException = new MemoryException();

        // Assert
        _ = acceleratorException.Should().NotBeAssignableTo<MemoryException>();
        _ = memoryException.Should().NotBeAssignableTo<AcceleratorException>();
    }

    [Fact]
    public void ExceptionTypes_ShouldHaveDistinctTypes()
    {
        // Arrange & Act
        var acceleratorType = typeof(AcceleratorException);
        var memoryType = typeof(MemoryException);
        var exceptionType = typeof(Exception);

        // Assert
        _ = acceleratorType.Should().NotBe(memoryType);
        _ = acceleratorType.Should().NotBe(exceptionType);
        _ = memoryType.Should().NotBe(exceptionType);

        _ = acceleratorType.IsSubclassOf(exceptionType).Should().BeTrue();
        _ = memoryType.IsSubclassOf(exceptionType).Should().BeTrue();
    }

    #endregion

    #region Exception Throwing and Catching Tests

    [Fact]
    public void ThrowingAcceleratorException_ShouldBeCatchableAsException()
    {
        // Arrange
        const string expectedMessage = "Test accelerator exception";
        Exception? caughtException;

        // Act
        try
        {
            throw new AcceleratorException(expectedMessage);
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // Assert
        Assert.NotNull(caughtException);
        _ = Assert.IsType<AcceleratorException>(caughtException);
        _ = caughtException!.Message.Should().Be(expectedMessage);
    }

    [Fact]
    public void ThrowingMemoryException_ShouldBeCatchableAsException()
    {
        // Arrange
        const string expectedMessage = "Test memory exception";
        Exception? caughtException;

        // Act
        try
        {
            throw new MemoryException(expectedMessage);
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // Assert
        Assert.NotNull(caughtException);
        _ = Assert.IsType<MemoryException>(caughtException);
        _ = caughtException!.Message.Should().Be(expectedMessage);
    }

    [Fact]
    public void ThrowingAcceleratorException_ShouldBeCatchableSpecifically()
    {
        // Arrange
        const string expectedMessage = "Specific accelerator error";
        AcceleratorException? caughtException;

        // Act
        try
        {
            throw new AcceleratorException(expectedMessage);
        }
        catch (AcceleratorException ex)
        {
            caughtException = ex;
        }

        // Assert
        Assert.NotNull(caughtException);
        _ = caughtException!.Message.Should().Be(expectedMessage);
    }

    [Fact]
    public void ThrowingMemoryException_ShouldBeCatchableSpecifically()
    {
        // Arrange
        const string expectedMessage = "Specific memory error";
        MemoryException? caughtException;

        // Act
        try
        {
            throw new MemoryException(expectedMessage);
        }
        catch (MemoryException ex)
        {
            caughtException = ex;
        }

        // Assert
        Assert.NotNull(caughtException);
        _ = caughtException!.Message.Should().Be(expectedMessage);
    }

    [Fact]
    public void ThrowingMemoryException_ShouldNotBeCatchableAsAcceleratorException()
    {
        // Arrange
        var memoryExceptionThrown = false;
        AcceleratorException? caughtAcceleratorException = null;
        MemoryException? caughtMemoryException = null;

        // Act
        try
        {
            try
            {
                throw new MemoryException("Memory error");
            }
            catch (AcceleratorException ex)
            {
                caughtAcceleratorException = ex;
            }
        }
        catch (MemoryException ex)
        {
            caughtMemoryException = ex;
            memoryExceptionThrown = true;
        }

        // Assert
        Assert.True(memoryExceptionThrown);
        Assert.Null(caughtAcceleratorException);
        Assert.NotNull(caughtMemoryException);
    }

    #endregion

    #region Exception Properties and Behavior Tests

    [Fact]
    public void ExceptionProperties_ShouldBehaveLikeBaseException()
    {
        // Arrange
        const string message = "Test message";
        var innerException = new InvalidOperationException("Inner");

        // Act
        var acceleratorException = new AcceleratorException(message, innerException);
        var memoryException = new MemoryException(message, innerException);

        // Assert - Should have standard Exception properties
        _ = acceleratorException.Message.Should().Be(message);
        _ = acceleratorException.InnerException.Should().Be(innerException);
        _ = acceleratorException.StackTrace.Should().NotBeNull();

        _ = memoryException.Message.Should().Be(message);
        _ = memoryException.InnerException.Should().Be(innerException);
        _ = memoryException.StackTrace.Should().NotBeNull();
    }

    [Fact]
    public void ExceptionToString_ShouldIncludeTypeAndMessage()
    {
        // Arrange
        const string message = "Test exception message";
        var acceleratorException = new AcceleratorException(message);
        var memoryException = new MemoryException(message);

        // Act
        var acceleratorString = acceleratorException.ToString();
        var memoryString = memoryException.ToString();

        // Assert
        _ = acceleratorString.Should().Contain(nameof(AcceleratorException));
        Assert.Contains(message, acceleratorString, StringComparison.Ordinal);

        _ = memoryString.Should().Contain(nameof(MemoryException));
        Assert.Contains(message, memoryString, StringComparison.Ordinal);
    }

    [Fact]
    public void ExceptionSource_ShouldBeSettable()
    {
        // Arrange
        var acceleratorException = new AcceleratorException("Test");
        var memoryException = new MemoryException("Test");
        const string expectedSource = "TestAssembly";

        // Act
        acceleratorException.Source = expectedSource;
        memoryException.Source = expectedSource;

        // Assert
        _ = acceleratorException.Source.Should().Be(expectedSource);
        _ = memoryException.Source.Should().Be(expectedSource);
    }

    #endregion

    #region Edge Cases and Error Conditions

    [Fact]
    public void Exceptions_WithVeryLongMessages_ShouldHandleCorrectly()
    {
        // Arrange
        var longMessage = new string('A', 10000); // 10k character message

        // Act
        var acceleratorException = new AcceleratorException(longMessage);
        var memoryException = new MemoryException(longMessage);

        // Assert
        _ = acceleratorException.Message.Should().Be(longMessage);
        _ = acceleratorException.Message.Length.Should().Be(10000);

        _ = memoryException.Message.Should().Be(longMessage);
        _ = memoryException.Message.Length.Should().Be(10000);
    }

    [Fact]
    public void Exceptions_WithSpecialCharacters_ShouldHandleCorrectly()
    {
        // Arrange
        const string specialMessage = "Error with special chars: \n\r\t\0\u00A9\u2122";

        // Act
        var acceleratorException = new AcceleratorException(specialMessage);
        var memoryException = new MemoryException(specialMessage);

        // Assert
        _ = acceleratorException.Message.Should().Be(specialMessage);
        _ = memoryException.Message.Should().Be(specialMessage);
    }

    [Fact]
    public void Exceptions_WithUnicodeMessages_ShouldHandleCorrectly()
    {
        // Arrange
        const string unicodeMessage = "错误消息 - エラーメッセージ - رسالة خطأ";

        // Act
        var acceleratorException = new AcceleratorException(unicodeMessage);
        var memoryException = new MemoryException(unicodeMessage);

        // Assert
        _ = acceleratorException.Message.Should().Be(unicodeMessage);
        _ = memoryException.Message.Should().Be(unicodeMessage);
    }

    #endregion

    #region Realistic Usage Scenarios

    [Fact]
    public void AcceleratorException_RealisticScenarios_ShouldWorkCorrectly()
    {
        // Test various realistic scenarios for AcceleratorException
        var scenarios = new[]
        {
        "CUDA driver not found",
        "Device compute capability insufficient(required: 7.0, found: 6.1)",
        "Failed to initialize OpenCL context",
        "Metal device not available on this system",
        "DirectML adapter initialization failed"
    };

        foreach (var scenario in scenarios)
        {
            var exception = new AcceleratorException(scenario);
            _ = exception.Message.Should().Be(scenario);
            _ = Assert.IsAssignableFrom<Exception>(exception);
        }
    }

    [Fact]
    public void MemoryException_RealisticScenarios_ShouldWorkCorrectly()
    {
        // Test various realistic scenarios for MemoryException
        var scenarios = new[]
        {
        "Insufficient GPU memory: required 4096MB, available 2048MB",
        "Memory allocation failed: out of device memory",
        "Buffer copy operation failed: invalid memory address",
        "Memory mapping not supported on this device",
        "Host memory allocation failed: system out of memory"
    };

        foreach (var scenario in scenarios)
        {
            var exception = new MemoryException(scenario);
            _ = exception.Message.Should().Be(scenario);
            _ = Assert.IsAssignableFrom<Exception>(exception);
        }
    }

    [Fact]
    public void Exceptions_InTypicalErrorHandlingPattern_ShouldWorkCorrectly()
    {
        // Arrange
        static void SimulateAcceleratorOperation()
        {
            throw new AcceleratorException("Simulated accelerator failure");
        }

        static void SimulateMemoryOperation()
        {
            throw new MemoryException("Simulated memory failure");
        }

        // Act & Assert
        var acceleratorAction = SimulateAcceleratorOperation;
        var memoryAction = SimulateMemoryOperation;

        _ = acceleratorAction.Should().Throw<AcceleratorException>()
                        .WithMessage("Simulated accelerator failure");

        _ = memoryAction.Should().Throw<MemoryException>()
                   .WithMessage("Simulated memory failure");
    }

    #endregion
}
