// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace DotCompute.Core.Common;

/// <summary>
/// Represents the result of an operation that may succeed or fail.
/// This is a non-generic result type for operations that don't return a value.
/// </summary>
public class Result
{
    /// <summary>
    /// Gets a value indicating whether the operation was successful.
    /// </summary>
    public bool IsSuccess { get; protected set; }

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the error message when the operation failed.
    /// </summary>
    public string Error { get; protected set; } = string.Empty;

    /// <summary>
    /// Gets the exception that caused the failure, if any.
    /// </summary>
    public Exception? Exception { get; protected set; }

    /// <summary>
    /// Protected constructor to ensure results are created through factory methods.
    /// </summary>
    protected Result(bool isSuccess, string error, Exception? exception = null)
    {
        IsSuccess = isSuccess;
        Error = error;
        Exception = exception;
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <returns>A successful result.</returns>
    public static Result Success() => new(true, string.Empty);

    /// <summary>
    /// Creates a failed result with an error message.
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <returns>A failed result.</returns>
    public static Result Failure(string error) => new(false, error);

    /// <summary>
    /// Creates a failed result with an error message and exception.
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <returns>A failed result.</returns>
    public static Result Failure(string error, Exception exception) => new(false, error, exception);

    /// <summary>
    /// Creates a failed result from an exception.
    /// </summary>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <returns>A failed result.</returns>
    public static Result Failure(Exception exception) => new(false, exception.Message, exception);

    /// <summary>
    /// Executes an action and returns the result.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <returns>A successful result if the action completed without throwing, otherwise a failed result.</returns>
    public static Result Try(Action action)
    {
        try
        {
            action();
            return Success();
        }
        catch (Exception ex)
        {
            return Failure(ex);
        }
    }

    /// <summary>
    /// Executes an async action and returns the result.
    /// </summary>
    /// <param name="action">The async action to execute.</param>
    /// <returns>A successful result if the action completed without throwing, otherwise a failed result.</returns>
    public static async Task<Result> TryAsync(Func<Task> action)
    {
        try
        {
            await action();
            return Success();
        }
        catch (Exception ex)
        {
            return Failure(ex);
        }
    }

    /// <summary>
    /// Combines multiple results into a single result.
    /// The combined result is successful only if all input results are successful.
    /// </summary>
    /// <param name="results">The results to combine.</param>
    /// <returns>A successful result if all inputs are successful, otherwise a failed result with combined error messages.</returns>
    public static Result Combine(params Result[] results)
    {
        if (results.Length == 0)
        {
            return Success();
        }


        var failures = results.Where(r => r.IsFailure).ToList();
        if (failures.Count == 0)
        {

            return Success();
        }


        var combinedError = string.Join("; ", failures.Select(f => f.Error));
        var firstException = failures.FirstOrDefault(f => f.Exception != null)?.Exception;

        return Failure(combinedError, firstException ?? new AggregateException("Multiple failures occurred"));
    }

    /// <summary>
    /// Implicitly converts a Result to a boolean indicating success.
    /// </summary>
    /// <param name="result">The result to convert.</param>
    /// <returns>True if the result is successful, false otherwise.</returns>
    public static implicit operator bool(Result result)
    {
        return result.IsSuccess;
    }

    /// <summary>
    /// Named alternate for implicit bool conversion.
    /// </summary>
    /// <param name="result">The result to convert.</param>
    /// <returns>True if the result is successful, false otherwise.</returns>
    public static bool ToBoolean(Result result) => result.IsSuccess;

    /// <summary>
    /// Returns a string representation of the result.
    /// </summary>
    /// <returns>A string describing the result state.</returns>
    public override string ToString() => IsSuccess ? "Success" : $"Failure: {Error}";
}

/// <summary>
/// Represents the result of an operation that may succeed or fail and returns a value.
/// This is a generic result type for operations that return a value on success.
/// </summary>
/// <typeparam name="T">The type of value returned on success.</typeparam>
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Static factory methods are the standard Result pattern for type-safe error handling")]
public class Result<T> : Result
{
    /// <summary>
    /// Gets the value when the operation was successful.
    /// </summary>
    public T Value { get; private set; } = default!;

    /// <summary>
    /// Protected constructor to ensure results are created through factory methods.
    /// </summary>
    private Result(bool isSuccess, T value, string error, Exception? exception = null)
        : base(isSuccess, error, exception)
    {
        Value = value;
    }

    /// <summary>
    /// Creates a successful result with a value.
    /// </summary>
    /// <param name="value">The value returned by the successful operation.</param>
    /// <returns>A successful result containing the value.</returns>
    public static Result<T> Success(T value) => new(true, value, string.Empty);

    /// <summary>
    /// Creates a failed result with an error message.
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <returns>A failed result.</returns>
    public new static Result<T> Failure(string error) => new(false, default!, error);

    /// <summary>
    /// Creates a failed result with an error message and exception.
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <returns>A failed result.</returns>
    public new static Result<T> Failure(string error, Exception exception) => new(false, default!, error, exception);

    /// <summary>
    /// Creates a failed result from an exception.
    /// </summary>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <returns>A failed result.</returns>
    public new static Result<T> Failure(Exception exception) => new(false, default!, exception.Message, exception);

    /// <summary>
    /// Executes a function and returns the result.
    /// </summary>
    /// <param name="func">The function to execute.</param>
    /// <returns>A successful result with the function's return value if it completed without throwing, otherwise a failed result.</returns>
    public static Result<T> Try(Func<T> func)
    {
        try
        {
            var value = func();
            return Success(value);
        }
        catch (Exception ex)
        {
            return Failure(ex);
        }
    }

    /// <summary>
    /// Executes an async function and returns the result.
    /// </summary>
    /// <param name="func">The async function to execute.</param>
    /// <returns>A successful result with the function's return value if it completed without throwing, otherwise a failed result.</returns>
    public static async Task<Result<T>> TryAsync(Func<Task<T>> func)
    {
        try
        {
            var value = await func();
            return Success(value);
        }
        catch (Exception ex)
        {
            return Failure(ex);
        }
    }

    /// <summary>
    /// Maps the value of a successful result to a new type.
    /// </summary>
    /// <typeparam name="TNew">The type to map to.</typeparam>
    /// <param name="mapper">The function to map the value.</param>
    /// <returns>A result with the mapped value if successful, otherwise the original failure.</returns>
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        if (IsFailure)
        {

            return Result<TNew>.Failure(Error, Exception!);
        }


        try
        {
            var mappedValue = mapper(Value);
            return Result<TNew>.Success(mappedValue);
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure("Mapping failed", ex);
        }
    }

    /// <summary>
    /// Binds the result to another operation that returns a result.
    /// </summary>
    /// <typeparam name="TNew">The type returned by the bound operation.</typeparam>
    /// <param name="binder">The function to bind to.</param>
    /// <returns>The result of the bound operation if this result is successful, otherwise the original failure.</returns>
    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder)
    {
        if (IsFailure)
        {

            return Result<TNew>.Failure(Error, Exception!);
        }


        try
        {
            return binder(Value);
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure("Binding failed", ex);
        }
    }

    /// <summary>
    /// Executes an action on the value if the result is successful.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <returns>The current result for chaining.</returns>
    public Result<T> OnSuccess(Action<T> action)
    {
        if (IsSuccess)
        {
            try
            {
                action(Value);
            }
            catch (Exception ex)
            {
                return Failure("OnSuccess action failed", ex);
            }
        }
        return this;
    }

    /// <summary>
    /// Executes an action on the error if the result is a failure.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <returns>The current result for chaining.</returns>
    public Result<T> OnFailure(Action<string> action)
    {
        if (IsFailure)
        {
            try
            {
                action(Error);
            }
            catch
            {
                // Ignore exceptions in error handling
            }
        }
        return this;
    }

    /// <summary>
    /// Gets the value if successful, otherwise returns the default value.
    /// </summary>
    /// <param name="defaultValue">The default value to return on failure.</param>
    /// <returns>The result value if successful, otherwise the default value.</returns>
    public T GetValueOrDefault(T defaultValue = default!) => IsSuccess ? Value : defaultValue;

    /// <summary>
    /// Gets the value if successful, otherwise throws an exception.
    /// </summary>
    /// <returns>The result value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the result is a failure.</exception>
#pragma warning disable CA1024 // Use properties where appropriate - Method throws exceptions
    public T GetValueOrThrow()
#pragma warning restore CA1024
    {
        if (IsFailure)
        {
            throw Exception ?? new InvalidOperationException(Error);
        }
        return Value;
    }

    /// <summary>
    /// Implicitly converts a value to a successful result.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>A successful result containing the value.</returns>
    public static implicit operator Result<T>(T value)
    {
        return Success(value);
    }

    /// <summary>
    /// Named alternate for implicit value conversion.
    /// </summary>
    /// <param name="value">The value to convert to a result.</param>
    /// <returns>A successful result containing the value.</returns>
    public static Result<T> FromT(T value) => Success(value);

    /// <summary>
    /// Implicitly converts a Result&lt;T&gt; to a boolean indicating success.
    /// </summary>
    /// <param name="result">The result to convert.</param>
    /// <returns>True if the result is successful, false otherwise.</returns>
    public static implicit operator bool(Result<T> result)
    {
        return result.IsSuccess;
    }

    /// <summary>
    /// Named alternate for implicit bool conversion.
    /// </summary>
    /// <param name="result">The result to convert.</param>
    /// <returns>True if the result is successful, false otherwise.</returns>
    public static bool ToBoolean(Result<T> result) => result.IsSuccess;

    /// <summary>
    /// Returns a string representation of the result.
    /// </summary>
    /// <returns>A string describing the result state and value.</returns>
    public override string ToString()
    {
        return IsSuccess
            ? $"Success: {Value}"
            : $"Failure: {Error}";
    }
}
