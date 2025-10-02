namespace DotCompute.Abstractions.Kernels;

/// <summary>
/// Represents a single argument passed to a kernel
/// </summary>
public sealed class KernelArgument
{
    /// <summary>
    /// Gets or sets the argument name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the argument type
    /// </summary>
    public Type Type { get; set; } = typeof(object);

    /// <summary>
    /// Gets or sets the argument value
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// Gets or sets the parameter direction (input, output, or bidirectional)
    /// </summary>
    public ParameterDirection Direction { get; set; } = ParameterDirection.In;

    /// <summary>
    /// Gets or sets the memory space where this argument resides
    /// </summary>
    public MemorySpace MemorySpace { get; set; } = MemorySpace.Global;

    /// <summary>
    /// Gets or sets the size of the argument in bytes
    /// </summary>
    public int Size { get; set; }

    /// <summary>
    /// Gets or sets the size of the argument in bytes (alias for Size).
    /// </summary>
    public int SizeInBytes
    {
        get => Size;
        set => Size = value;
    }

    /// <summary>
    /// Gets or sets whether this argument is a pointer/reference type
    /// </summary>
    public bool IsPointer { get; set; }

    /// <summary>
    /// Gets or sets whether this argument is constant
    /// </summary>
    public bool IsConstant { get; set; }

    /// <summary>
    /// Gets or sets the alignment requirement for this argument
    /// </summary>
    public int Alignment { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether this argument represents device memory
    /// </summary>
    public bool IsDeviceMemory { get; set; }

    /// <summary>
    /// Gets or sets the memory buffer associated with this argument
    /// </summary>
    public object? MemoryBuffer { get; set; }

    /// <summary>
    /// Gets or sets whether this argument is an output parameter
    /// </summary>
    public bool IsOutput { get; set; }

    /// <summary>
    /// Creates a new KernelArgument
    /// </summary>
    public KernelArgument() { }

    /// <summary>
    /// Creates a new KernelArgument with the specified name and value
    /// </summary>
    /// <param name="name">The argument name</param>
    /// <param name="value">The argument value</param>
    public KernelArgument(string name, object? value)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Value = value;
        Type = value?.GetType() ?? typeof(object);

        if (value != null)
        {
            Size = CalculateSize(value);
            IsPointer = IsPointerType(Type);
        }
    }

    /// <summary>
    /// Creates a new KernelArgument with the specified name, type, and value
    /// </summary>
    /// <param name="name">The argument name</param>
    /// <param name="type">The argument type</param>
    /// <param name="value">The argument value</param>
    public KernelArgument(string name, Type type, object? value)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Value = value;

        if (value != null)
        {
            Size = CalculateSize(value);
        }

        IsPointer = IsPointerType(type);
    }

    /// <summary>
    /// Creates a typed kernel argument
    /// </summary>
    /// <typeparam name="T">The argument type</typeparam>
    /// <param name="name">The argument name</param>
    /// <param name="value">The argument value</param>
    /// <returns>A new KernelArgument</returns>
    public static KernelArgument Create<T>(string name, T value)
    {
        return new KernelArgument(name, typeof(T), value);
    }

    /// <summary>
    /// Creates a pointer/buffer argument
    /// </summary>
    /// <param name="name">The argument name</param>
    /// <param name="buffer">The buffer</param>
    /// <param name="direction">The parameter direction</param>
    /// <returns>A new KernelArgument</returns>
    public static KernelArgument CreateBuffer(string name, object buffer, ParameterDirection direction = ParameterDirection.In)
    {
        return new KernelArgument(name, buffer)
        {
            Direction = direction,
            IsPointer = true,
            MemorySpace = MemorySpace.Global
        };
    }

    /// <summary>
    /// Calculates the size of a value in bytes
    /// </summary>
    private static int CalculateSize(object value)
    {
        return value switch
        {
            byte => 1,
            sbyte => 1,
            short => 2,
            ushort => 2,
            int => 4,
            uint => 4,
            long => 8,
            ulong => 8,
            float => 4,
            double => 8,
            bool => 1,
            char => 2,
            Array array => array.Length * GetElementSize(array.GetType().GetElementType()!),
            _ => IntPtr.Size
        };
    }

    /// <summary>
    /// Gets the size of an element type
    /// </summary>
    private static int GetElementSize(Type elementType)
    {
        if (elementType == typeof(byte) || elementType == typeof(sbyte))
        {
            return 1;
        }


        if (elementType == typeof(short) || elementType == typeof(ushort))
        {
            return 2;
        }


        if (elementType == typeof(int) || elementType == typeof(uint) || elementType == typeof(float))
        {
            return 4;
        }


        if (elementType == typeof(long) || elementType == typeof(ulong) || elementType == typeof(double))
        {
            return 8;
        }


        if (elementType == typeof(bool))
        {
            return 1;
        }


        if (elementType == typeof(char))
        {
            return 2;
        }


        return IntPtr.Size;
    }

    /// <summary>
    /// Determines if a type is a pointer type
    /// </summary>
    private static bool IsPointerType(Type type)
    {
        return type.IsPointer ||
               type.IsArray ||
               type.Name.Contains("Span") ||
               type.Name.Contains("Memory") ||
               type.Name.Contains("Buffer");
    }

    /// <summary>
    /// Returns a string representation of the argument
    /// </summary>
    public override string ToString()
    {
        return $"{Name}: {Type.Name} = {Value}";
    }
}