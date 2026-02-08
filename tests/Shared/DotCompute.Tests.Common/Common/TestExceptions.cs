// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Tests.Common;

/// <summary>
/// Custom test exception to replace StackOverflowException in tests.
/// Using reserved exception types like StackOverflowException in test code
/// violates CA2201 as they are reserved for runtime use only.
/// </summary>
public sealed class TestStackOverflowException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestStackOverflowException"/> class.
    /// </summary>
    public TestStackOverflowException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestStackOverflowException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public TestStackOverflowException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestStackOverflowException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="inner">The exception that is the cause of the current exception.</param>
    public TestStackOverflowException(string message, Exception inner) : base(message, inner)
    {
    }
}

/// <summary>
/// Custom test exception to replace OutOfMemoryException in tests.
/// Using reserved exception types like OutOfMemoryException in test code
/// violates CA2201 as they are reserved for runtime use only.
/// </summary>
public sealed class TestOutOfMemoryException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestOutOfMemoryException"/> class.
    /// </summary>
    public TestOutOfMemoryException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestOutOfMemoryException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public TestOutOfMemoryException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestOutOfMemoryException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="inner">The exception that is the cause of the current exception.</param>
    public TestOutOfMemoryException(string message, Exception inner) : base(message, inner)
    {
    }
}

/// <summary>
/// Custom test exception to replace AccessViolationException in tests.
/// Using reserved exception types like AccessViolationException in test code
/// violates CA2201 as they are reserved for runtime use only.
/// </summary>
public sealed class TestAccessViolationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TestAccessViolationException"/> class.
    /// </summary>
    public TestAccessViolationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestAccessViolationException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public TestAccessViolationException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TestAccessViolationException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="inner">The exception that is the cause of the current exception.</param>
    public TestAccessViolationException(string message, Exception inner) : base(message, inner)
    {
    }
}
