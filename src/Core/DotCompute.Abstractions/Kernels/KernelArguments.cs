// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections;

namespace DotCompute.Abstractions.Kernels;

/// <summary>
/// Represents kernel execution arguments that are passed to a compute kernel during execution.
/// Provides type-safe access to arguments with validation and error handling.
/// </summary>
public class KernelArguments : IEnumerable<object?>
{
    private readonly List<object?> _arguments;

    /// <summary>
    /// Gets the number of arguments currently stored in this instance.
    /// </summary>
    /// <value>The total count of arguments.</value>
    public int Count => _arguments.Count;


    /// <summary>
    /// Gets the number of arguments (alias for Count for compatibility).
    /// </summary>
    /// <value>The total length of arguments, identical to Count.</value>
    public int Length => _arguments.Count;


    /// <summary>
    /// Initializes a new instance of the <see cref="KernelArguments"/> class with an empty argument list.
    /// </summary>
    public KernelArguments()
    {
        _arguments = new List<object?>();
    }


    /// <summary>
    /// Initializes a new instance of the <see cref="KernelArguments"/> class with the specified initial capacity.
    /// The arguments list will be pre-filled with null values to match the capacity.
    /// </summary>
    /// <param name="capacity">The initial capacity for arguments.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="capacity"/> is negative.</exception>
    public KernelArguments(int capacity)
    {
        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity cannot be negative.");
        }

        _arguments = new List<object?>(capacity);

        // Pre-fill with null values to match the expected capacity

        for (var i = 0; i < capacity; i++)
        {
            _arguments.Add(null);
        }
    }


    /// <summary>
    /// Initializes a new instance of the <see cref="KernelArguments"/> class with the specified initial arguments.
    /// </summary>
    /// <param name="arguments">The initial arguments to store.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="arguments"/> is null.</exception>
    public KernelArguments(params object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        _arguments = new List<object?>(arguments);
    }

    /// <summary>
    /// Adds an argument to the end of the argument list.
    /// </summary>
    /// <param name="argument">The argument to add. Can be null.</param>
    public void Add(object? argument) => _arguments.Add(argument);

    /// <summary>
    /// Sets the argument at the specified index to the given value.
    /// </summary>
    /// <param name="index">The zero-based index of the argument to set.</param>
    /// <param name="value">The value to set at the specified index. Can be null.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is negative or greater than or equal to Count.</exception>
    /// <exception cref="InvalidOperationException">Thrown when trying to set an argument on an uninitialized KernelArguments instance.</exception>
    public void Set(int index, object? value)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Index cannot be negative.");
        }

        if (index >= _arguments.Count)
        {
            // If we're in an uninitialized state (Count == 0), throw a more informative exception
            if (_arguments.Count == 0)
            {
                throw new InvalidOperationException($"Cannot set argument at index {index}. KernelArguments has not been initialized with a capacity. Use KernelArguments.Create(capacity) or the constructor with capacity parameter.");
            }

            throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} is out of range. Valid range is 0 to {_arguments.Count - 1}.");
        }


        _arguments[index] = value;
    }

    /// <summary>
    /// Gets or sets the argument at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the argument to get or set.</param>
    /// <returns>The argument at the specified index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is negative or greater than or equal to Count.</exception>
    /// <exception cref="InvalidOperationException">Thrown when trying to access arguments on an uninitialized KernelArguments instance.</exception>
    public object? this[int index]
    {

        get
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Index cannot be negative.");
            }

            if (index >= _arguments.Count)
            {
                // If we're in an uninitialized state (Count == 0), throw a more informative exception
                if (_arguments.Count == 0)
                {
                    throw new InvalidOperationException($"Cannot get argument at index {index}. KernelArguments has not been initialized with a capacity. Use KernelArguments.Create(capacity) or the constructor with capacity parameter.");
                }

                throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} is out of range. Valid range is 0 to {_arguments.Count - 1}.");
            }


            return _arguments[index];
        }
        set => Set(index, value);
    }

    /// <summary>
    /// Gets a read-only view of all arguments currently stored in this instance.
    /// </summary>
    /// <value>A read-only list containing all arguments.</value>
    public IReadOnlyList<object?> Arguments => _arguments.AsReadOnly();


    /// <summary>
    /// Gets the argument at the specified index with type safety and automatic casting.
    /// </summary>
    /// <typeparam name="T">The expected type of the argument.</typeparam>
    /// <param name="index">The zero-based index of the argument to get.</param>
    /// <returns>The argument at the specified index cast to the specified type.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is negative or greater than or equal to Count.</exception>
    /// <exception cref="InvalidOperationException">Thrown when trying to access arguments on an uninitialized KernelArguments instance.</exception>
    /// <exception cref="InvalidCastException">Thrown when the argument cannot be cast to the specified type.</exception>
    public T Get<T>(int index)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Index cannot be negative.");
        }

        if (index >= _arguments.Count)
        {
            // If we're in an uninitialized state (Count == 0), throw a more informative exception
            if (_arguments.Count == 0)
            {
                throw new InvalidOperationException($"Cannot get argument at index {index}. KernelArguments has not been initialized with a capacity. Use KernelArguments.Create(capacity) or the constructor with capacity parameter.");
            }

            throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} is out of range. Valid range is 0 to {_arguments.Count - 1}.");
        }


        var value = _arguments[index];


        if (value == null)
        {
            // Handle nullable reference types and value types
            if (typeof(T).IsValueType && Nullable.GetUnderlyingType(typeof(T)) == null)
            {
                throw new InvalidCastException($"Cannot cast null value to non-nullable value type {typeof(T).Name}.");
            }
            return default(T)!;
        }


        if (value is T typedValue)
        {
            return typedValue;
        }

        // Try explicit conversion

        try
        {
            return (T)value;
        }
        catch (InvalidCastException ex)
        {
            throw new InvalidCastException($"Cannot cast argument at index {index} of type {value.GetType().Name} to type {typeof(T).Name}.", ex);
        }
    }

    /// <summary>
    /// Gets the argument at the specified index without type conversion.
    /// </summary>
    /// <param name="index">The zero-based index of the argument to get.</param>
    /// <returns>The raw argument at the specified index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is negative or greater than or equal to Count.</exception>
    /// <exception cref="InvalidOperationException">Thrown when trying to access arguments on an uninitialized KernelArguments instance.</exception>
    public object? Get(int index) => this[index];

    /// <summary>
    /// Creates a new <see cref="KernelArguments"/> instance with the specified initial capacity.
    /// The arguments list will be pre-filled with null values.
    /// </summary>
    /// <param name="capacity">The initial capacity for arguments.</param>
    /// <returns>A new KernelArguments instance with the specified capacity.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="capacity"/> is negative.</exception>
    public static KernelArguments Create(int capacity) => new KernelArguments(capacity);

    /// <summary>
    /// Creates a new <see cref="KernelArguments"/> instance with the specified arguments.
    /// </summary>
    /// <param name="arguments">The initial arguments to store.</param>
    /// <returns>A new KernelArguments instance with the specified arguments.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="arguments"/> is null.</exception>
    public static KernelArguments Create(params object?[] arguments) => new KernelArguments(arguments);

    /// <summary>
    /// Removes all arguments from this instance, resetting the Count to zero.
    /// </summary>
    public void Clear() => _arguments.Clear();

    /// <summary>
    /// Returns an enumerator that iterates through the arguments.
    /// </summary>
    /// <returns>An enumerator for the arguments.</returns>
    public IEnumerator<object?> GetEnumerator() => _arguments.GetEnumerator();

    /// <summary>
    /// Returns an enumerator that iterates through the arguments.
    /// </summary>
    /// <returns>An enumerator for the arguments.</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}