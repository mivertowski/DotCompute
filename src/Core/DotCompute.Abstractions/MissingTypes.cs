// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.ComponentModel;

namespace DotCompute.Abstractions;

/// <summary>
/// Represents a 3D dimension structure.
/// </summary>
[TypeConverter(typeof(Dim3TypeConverter))]
public readonly struct Dim3 : IEquatable<Dim3>
{
    /// <summary>Gets the X dimension.</summary>
    public int X { get; }

    /// <summary>Gets the Y dimension.</summary>
    public int Y { get; }

    /// <summary>Gets the Z dimension.</summary>
    public int Z { get; }

    /// <summary>
    /// Initializes a new instance of the Dim3 struct.
    /// </summary>
    public Dim3(int x, int y = 1, int z = 1)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>Gets the total number of elements.</summary>
    public int Length => X * Y * Z;

    /// <summary>Returns true if any dimension is zero or negative.</summary>
    public bool IsEmpty => X <= 0 || Y <= 0 || Z <= 0;

    public bool Equals(Dim3 other) => X == other.X && Y == other.Y && Z == other.Z;
    public override bool Equals(object? obj) => obj is Dim3 other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);
    public override string ToString() => $"({X}, {Y}, {Z})";

    public static bool operator ==(Dim3 left, Dim3 right) => left.Equals(right);
    public static bool operator !=(Dim3 left, Dim3 right) => !left.Equals(right);

    public static implicit operator Dim3(int value) => new(value);
    public static implicit operator Dim3((int x, int y) tuple) => new(tuple.x, tuple.y);
    public static implicit operator Dim3((int x, int y, int z) tuple) => new(tuple.x, tuple.y, tuple.z);
    
    /// <summary>
    /// Creates a Dim3 from an integer value.
    /// </summary>
    public static Dim3 FromInt32(int value) => new(value);
    
    /// <summary>
    /// Creates a Dim3 from a two-component tuple.
    /// </summary>
    public static Dim3 FromValueTuple((int x, int y) tuple) => new(tuple.x, tuple.y);
    
    /// <summary>
    /// Creates a Dim3 from a three-component tuple.
    /// </summary>
    public static Dim3 FromValueTuple((int x, int y, int z) tuple) => new(tuple.x, tuple.y, tuple.z);
}

/// <summary>
/// Type converter for Dim3.
/// </summary>
public class Dim3TypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
}

/// <summary>
/// Represents a kernel definition.
/// </summary>
public class KernelDefinition
{
    /// <summary>Gets or sets the kernel name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets or sets the kernel source code.</summary>
    public string? Source { get; init; }
    
    /// <summary>Gets or sets the kernel code (alias for Source for compatibility).</summary>
    public string? Code 
    { 
        get => Source; 
        init => Source = value; 
    }

    /// <summary>Gets or sets the entry point function name.</summary>
    public string EntryPoint { get; init; } = "main";

    /// <summary>Gets or sets kernel metadata.</summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
    
    /// <summary>
    /// Initializes a new instance of the KernelDefinition class.
    /// </summary>
    public KernelDefinition() { }
    
    /// <summary>
    /// Initializes a new instance of the KernelDefinition class.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public KernelDefinition(string name, string source, string? entryPoint = null)
    {
        Name = name;
        Source = source;
        EntryPoint = entryPoint ?? "main";
    }
}

/// <summary>
/// Represents kernel execution arguments.
/// </summary>
public class KernelArguments
{
    private readonly List<object?> _arguments;

    /// <summary>Gets the number of arguments.</summary>
    public int Count => _arguments.Count;
    
    /// <summary>Gets the number of arguments (alias for Count).</summary>
    public int Length => _arguments.Count;
    
    /// <summary>
    /// Initializes a new instance of KernelArguments.
    /// </summary>
    public KernelArguments()
    {
        _arguments = new List<object?>();
    }
    
    /// <summary>
    /// Initializes a new instance of KernelArguments with initial capacity.
    /// </summary>
    /// <param name="capacity">The initial capacity for arguments.</param>
    public KernelArguments(int capacity)
    {
        if (capacity < 0)
        {

            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity cannot be negative.");
        }


        _arguments = new List<object?>(capacity);
        
        // Pre-fill with null values to match the expected capacity
        for (int i = 0; i < capacity; i++)
        {
            _arguments.Add(null);
        }
    }
    
    /// <summary>
    /// Initializes a new instance of KernelArguments with initial arguments.
    /// </summary>
    /// <param name="arguments">The initial arguments.</param>
    public KernelArguments(params object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        _arguments = new List<object?>(arguments);
    }

    /// <summary>
    /// Adds an argument to the kernel.
    /// </summary>
    /// <param name="argument">The argument to add.</param>
    public void Add(object? argument) => _arguments.Add(argument);

    /// <summary>
    /// Sets the argument at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the argument to set.</param>
    /// <param name="value">The value to set.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is out of range.</exception>
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
    /// Gets the argument at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the argument to get.</param>
    /// <returns>The argument at the specified index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is out of range.</exception>
    /// <exception cref="InvalidOperationException">Thrown when trying to access arguments on an uninitialized KernelArguments.</exception>
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
    }

    /// <summary>Gets all arguments.</summary>
    public IReadOnlyList<object?> Arguments => _arguments.AsReadOnly();
    
    /// <summary>
    /// Gets the argument at the specified index with type safety.
    /// </summary>
    /// <typeparam name="T">The expected type of the argument.</typeparam>
    /// <param name="index">The zero-based index of the argument to get.</param>
    /// <returns>The argument at the specified index cast to the specified type.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is out of range.</exception>
    /// <exception cref="InvalidOperationException">Thrown when trying to access arguments on an uninitialized KernelArguments.</exception>
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
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is out of range.</exception>
    /// <exception cref="InvalidOperationException">Thrown when trying to access arguments on an uninitialized KernelArguments.</exception>
    public object? Get(int index) => this[index];

    /// <summary>
    /// Creates a new KernelArguments instance with the specified initial capacity.
    /// </summary>
    /// <param name="capacity">The initial capacity for arguments.</param>
    /// <returns>A new KernelArguments instance with the specified capacity.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when capacity is negative.</exception>
    public static KernelArguments Create(int capacity) => new KernelArguments(capacity);


    /// <summary>
    /// Creates a new KernelArguments instance with the specified arguments.
    /// </summary>
    /// <param name="arguments">The initial arguments.</param>
    /// <returns>A new KernelArguments instance with the specified arguments.</returns>
    public static KernelArguments Create(params object?[] arguments) => new KernelArguments(arguments);


    /// <summary>
    /// Clears all arguments from this instance.
    /// </summary>
    public void Clear() => _arguments.Clear();


    /// <summary>
    /// Returns an enumerator that iterates through the arguments.
    /// </summary>
    /// <returns>An enumerator for the arguments.</returns>
    public IEnumerator<object?> GetEnumerator() => _arguments.GetEnumerator();
}

/// <summary>
/// Defines accelerator features that may or may not be supported.
/// </summary>
[Flags]
public enum AcceleratorFeature
{
    None = 0,
    Float16 = 1 << 0,
    DoublePrecision = 1 << 1,
    LongInteger = 1 << 2,
    TensorCores = 1 << 3,
    UnifiedMemory = 1 << 4,
    CooperativeGroups = 1 << 5,
    DynamicParallelism = 1 << 6,
    AtomicOperations = 1 << 7,
    Bfloat16 = 1 << 8,
    SignedByte = 1 << 9,
    MixedPrecision = 1 << 10
}

/// <summary>
/// Represents a compiled kernel that can be executed.
/// </summary>
public class CompiledKernel : IDisposable
{
    /// <summary>Gets the kernel name.</summary>
    public required string Name { get; init; }
    
    /// <summary>Gets the kernel id.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>Gets the compiled binary or bytecode.</summary>
    public byte[]? CompiledBinary { get; init; }

    /// <summary>Gets the compilation metadata.</summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>Gets whether the kernel is disposed.</summary>
    public bool IsDisposed { get; private set; }
    
    /// <summary>
    /// Initializes a new instance of CompiledKernel.
    /// </summary>
    public CompiledKernel() { }
    
    /// <summary>
    /// Initializes a new instance of CompiledKernel with name, binary, compilation options, and metadata.
    /// </summary>
    public CompiledKernel(string name, byte[]? binary, CompilationOptions? options = null, Dictionary<string, object>? metadata = null)
    {
        Name = name;
        CompiledBinary = binary;
        Metadata = metadata ?? new Dictionary<string, object>();
        
        if (options != null)
        {
            Metadata["CompilationOptions"] = options;
        }
    }

    /// <summary>
    /// Initializes a new instance of CompiledKernel for low-level usage with native handle.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public CompiledKernel(Guid id, IntPtr nativeHandle, int sharedMemorySize, KernelConfiguration configuration)
    {
        Name = $"Kernel_{id:N}";
        Id = id.ToString();
        CompiledBinary = null; // No binary for native handle kernels
        Metadata = new Dictionary<string, object>
        {
            ["Id"] = id,
            ["NativeHandle"] = nativeHandle,
            ["SharedMemorySize"] = sharedMemorySize,
            ["Configuration"] = configuration
        };
    }

    /// <summary>
    /// Executes the compiled kernel with the specified arguments.
    /// </summary>
    public virtual Task ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        if (IsDisposed)
        {

            throw new ObjectDisposedException(nameof(CompiledKernel));
        }

        // Default implementation - would be overridden by backend-specific implementations

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (!IsDisposed)
        {
            IsDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
}

/// <summary>
/// Represents kernel configuration settings.
/// </summary>
public class KernelConfiguration
{
    /// <summary>Gets or sets the kernel name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the optimization level.</summary>
    public OptimizationLevel OptimizationLevel { get; set; } = OptimizationLevel.Default;

    /// <summary>Gets or sets kernel-specific options.</summary>
    public Dictionary<string, object> Options { get; set; } = new();
    
    /// <summary>
    /// Initializes a new instance of KernelConfiguration.
    /// </summary>
    public KernelConfiguration() { }
    
    /// <summary>
    /// Initializes a new instance of KernelConfiguration.
    /// </summary>
    public KernelConfiguration(Dim3 gridDim, Dim3 blockDim)
    {
        Options["GridDimension"] = gridDim;
        Options["BlockDimension"] = blockDim;
    }
}