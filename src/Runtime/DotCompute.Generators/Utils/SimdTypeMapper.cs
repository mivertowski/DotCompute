// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Generators.Utils;

/// <summary>
/// Maps element types to SIMD vector types and intrinsic operations.
/// </summary>
public static class SimdTypeMapper
{
    /// <summary>
    /// Configuration for SIMD type mapping.
    /// </summary>
    public class SimdConfiguration
    {
        public bool PreferAvx2 { get; set; } = true;
        public bool PreferAvx512 { get; set; }

        public bool FallbackToSse { get; set; } = true;
        public int DefaultVectorBitWidth { get; set; } = 256; // AVX2 default
    }

    private static readonly Dictionary<string, Type> ElementTypeToNetType = new()
    {
        ["float"] = typeof(float),
        ["double"] = typeof(double),
        ["int"] = typeof(int),
        ["uint"] = typeof(uint),
        ["long"] = typeof(long),
        ["ulong"] = typeof(ulong),
        ["short"] = typeof(short),
        ["ushort"] = typeof(ushort),
        ["byte"] = typeof(byte),
        ["sbyte"] = typeof(sbyte),
        ["nint"] = typeof(nint),
        ["nuint"] = typeof(nuint)
    };

    /// <summary>
    /// Gets the appropriate SIMD type for the given element type and vector size.
    /// </summary>
    /// <param name="elementType">The element type (e.g., "float", "int").</param>
    /// <param name="vectorSize">The desired vector size in bytes.</param>
    /// <returns>The SIMD type name as a string.</returns>
    public static string GetSimdType(string elementType, int vectorSize)
    {
        ArgumentValidation.ThrowIfNullOrEmpty(elementType);


        if (vectorSize <= 0)
        {
            throw new ArgumentException("Vector size must be positive", nameof(vectorSize));
        }

        if (!IsSupportedSimdType(elementType))
        {
            throw new NotSupportedException($"SIMD type not supported for {elementType}");
        }

        var vectorBits = vectorSize * 8;

        // Use generic Vector<T> for standard sizes
        // Note: In source generators (netstandard2.0), we generate code for runtime that may support these types

        if (vectorBits == 128)
        {
            return $"Vector128<{elementType}>";
        }


        if (vectorBits == 256)
        {
            return $"Vector256<{elementType}>";
        }


        if (vectorBits == 512)
        {
            return $"Vector512<{elementType}>"; // For future AVX-512 support
        }

        // Fall back to generic vector
        return $"Vector<{elementType}>";
    }

    /// <summary>
    /// Gets the intrinsic operation for the given operation and vector type.
    /// </summary>
    /// <param name="operation">The operation (e.g., "+", "-", "min").</param>
    /// <param name="vectorType">The vector type name.</param>
    /// <returns>The intrinsic operation as a string.</returns>
    public static string GetIntrinsicOperation(string operation, string vectorType)
    {
        ArgumentValidation.ThrowIfNullOrEmpty(operation);
        ArgumentValidation.ThrowIfNullOrEmpty(vectorType);

        // Extract base vector type (e.g., Vector256 from Vector256<float>)
        var baseVectorType = ExtractBaseVectorType(vectorType);


        return operation switch
        {
            "+" => $"{baseVectorType}.Add",
            "-" => $"{baseVectorType}.Subtract",
            "*" => $"{baseVectorType}.Multiply",
            "/" => $"{baseVectorType}.Divide",
            "%" => $"{baseVectorType}.Remainder",
            "&" => $"{baseVectorType}.BitwiseAnd",
            "|" => $"{baseVectorType}.BitwiseOr",
            "^" => $"{baseVectorType}.Xor",
            "<<" => $"{baseVectorType}.ShiftLeft",
            ">>" => $"{baseVectorType}.ShiftRightArithmetic",
            ">>>" => $"{baseVectorType}.ShiftRightLogical",
            "min" => $"{baseVectorType}.Min",
            "max" => $"{baseVectorType}.Max",
            "sqrt" => $"{baseVectorType}.SquareRoot",
            "abs" => $"{baseVectorType}.Abs",
            "negate" => $"{baseVectorType}.Negate",
            "ceiling" => $"{baseVectorType}.Ceiling",
            "floor" => $"{baseVectorType}.Floor",
            "round" => $"{baseVectorType}.Round",
            "fma" => $"{baseVectorType}.FusedMultiplyAdd",
            "dot" => $"{baseVectorType}.Dot",
            _ => throw new NotSupportedException($"Intrinsic operation not supported for {operation}")
        };
    }

    /// <summary>
    /// Gets the optimal vector size for the given element type.
    /// </summary>
    /// <param name="elementType">The element type.</param>
    /// <param name="config">Optional SIMD configuration.</param>
    /// <returns>Optimal vector size in bytes.</returns>
    public static int GetOptimalVectorSize(string elementType, SimdConfiguration? config = null)
    {
        ArgumentValidation.ThrowIfNullOrEmpty(elementType);


        config ??= new SimdConfiguration();
        _ = GetElementSizeInBytes(elementType);
        var targetBits = config.DefaultVectorBitWidth;

        // Check hardware support

        if (config.PreferAvx512 && IsAvx512Supported())
        {
            targetBits = 512;
        }
        else if (config.PreferAvx2 && IsAvx2Supported())
        {
            targetBits = 256;
        }
        else if (config.FallbackToSse && IsSseSupported())
        {
            targetBits = 128;
        }


        return targetBits / 8;
    }

    /// <summary>
    /// Determines if the given element type supports SIMD operations.
    /// </summary>
    /// <param name="elementType">The element type to check.</param>
    /// <returns>True if SIMD is supported, false otherwise.</returns>
    public static bool IsSupportedSimdType(string elementType) => ElementTypeToNetType.ContainsKey(elementType);

    /// <summary>
    /// Gets the size of an element type in bytes.
    /// </summary>
    /// <param name="elementType">The element type.</param>
    /// <returns>Size in bytes.</returns>
    public static int GetElementSizeInBytes(string elementType)
    {
        return elementType switch
        {
            "double" or "long" or "ulong" => 8,
            "float" or "int" or "uint" => 4,
            "short" or "ushort" => 2,
            "byte" or "sbyte" => 1,
            "nint" or "nuint" => IntPtr.Size,
            _ => throw new NotSupportedException($"Unknown element type: {elementType}")
        };
    }

    /// <summary>
    /// Gets the size of an element type in bits.
    /// </summary>
    /// <param name="elementType">The element type.</param>
    /// <returns>Size in bits.</returns>
    public static int GetElementSizeInBits(string elementType) => GetElementSizeInBytes(elementType) * 8;

    /// <summary>
    /// Gets the number of elements that fit in a vector of the given size.
    /// </summary>
    /// <param name="elementType">The element type.</param>
    /// <param name="vectorSizeBytes">Vector size in bytes.</param>
    /// <returns>Number of elements.</returns>
    public static int GetVectorElementCount(string elementType, int vectorSizeBytes)
    {
        var elementSize = GetElementSizeInBytes(elementType);
        return vectorSizeBytes / elementSize;
    }

    /// <summary>
    /// Generates code for loading data into a SIMD vector.
    /// </summary>
    /// <param name="vectorType">The vector type.</param>
    /// <param name="sourceExpression">Expression for the data source.</param>
    /// <param name="offset">Offset in the source.</param>
    /// <returns>Load operation code.</returns>
    public static string GenerateVectorLoad(string vectorType, string sourceExpression, string offset)
    {
        ArgumentValidation.ThrowIfNullOrEmpty(vectorType);
        ArgumentValidation.ThrowIfNullOrEmpty(sourceExpression);
        ArgumentValidation.ThrowIfNullOrEmpty(offset);


        var baseType = ExtractBaseVectorType(vectorType);
        return $"{baseType}.Load({sourceExpression}, {offset})";
    }

    /// <summary>
    /// Generates code for storing a SIMD vector to memory.
    /// </summary>
    /// <param name="vectorExpression">The vector expression to store.</param>
    /// <param name="destinationExpression">Expression for the destination.</param>
    /// <param name="offset">Offset in the destination.</param>
    /// <returns>Store operation code.</returns>
    public static string GenerateVectorStore(string vectorExpression, string destinationExpression, string offset)
    {
        ArgumentValidation.ThrowIfNullOrEmpty(vectorExpression);
        ArgumentValidation.ThrowIfNullOrEmpty(destinationExpression);
        ArgumentValidation.ThrowIfNullOrEmpty(offset);


        return $"{vectorExpression}.Store({destinationExpression}, {offset})";
    }

    /// <summary>
    /// Gets platform-specific intrinsic for an operation.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="platform">Target platform (e.g., "X86", "Arm").</param>
    /// <param name="vectorWidth">Vector width in bits.</param>
    /// <returns>Platform-specific intrinsic name.</returns>
    public static string GetPlatformIntrinsic(string operation, string platform, int vectorWidth)
    {
        ArgumentValidation.ThrowIfNullOrEmpty(operation);
        ArgumentValidation.ThrowIfNullOrEmpty(platform);


        if (platform.Equals("X86", StringComparison.OrdinalIgnoreCase))
        {
            return GetX86Intrinsic(operation, vectorWidth);
        }


        if (platform.Equals("Arm", StringComparison.OrdinalIgnoreCase))
        {
            return GetArmIntrinsic(operation, vectorWidth);
        }


        throw new NotSupportedException($"Platform {platform} not supported");
    }

    /// <summary>
    /// Extracts the base vector type from a generic vector type string.
    /// </summary>
    private static string ExtractBaseVectorType(string vectorType)
    {
        var genericIndex = vectorType.IndexOf('<');
        return genericIndex > 0 ? vectorType.Substring(0, genericIndex) : vectorType;
    }

    /// <summary>
    /// Gets X86-specific intrinsic.
    /// </summary>
    private static string GetX86Intrinsic(string operation, int vectorWidth)
    {
        var prefix = vectorWidth switch
        {
            128 => "Sse",
            256 => "Avx",
            512 => "Avx512F",
            _ => throw new NotSupportedException($"Vector width {vectorWidth} not supported for X86")
        };


        return operation switch
        {
            "+" => $"{prefix}.Add",
            "-" => $"{prefix}.Subtract",
            "*" => $"{prefix}.Multiply",
            "/" => $"{prefix}.Divide",
            "sqrt" => $"{prefix}.Sqrt",
            "reciprocal" => $"{prefix}.Reciprocal",
            "rsqrt" => $"{prefix}.ReciprocalSqrt",
            _ => throw new NotSupportedException($"Operation {operation} not supported for {prefix}")
        };
    }

    /// <summary>
    /// Gets ARM-specific intrinsic.
    /// </summary>
    private static string GetArmIntrinsic(string operation, int vectorWidth)
    {
        if (vectorWidth != 128)
        {
            throw new NotSupportedException($"Vector width {vectorWidth} not supported for ARM");
        }


        return operation switch
        {
            "+" => "AdvSimd.Add",
            "-" => "AdvSimd.Subtract",
            "*" => "AdvSimd.Multiply",
            "min" => "AdvSimd.Min",
            "max" => "AdvSimd.Max",
            "abs" => "AdvSimd.Abs",
            _ => throw new NotSupportedException($"Operation {operation} not supported for AdvSimd")
        };
    }

    /// <summary>
    /// Checks if AVX2 is supported on the current hardware.
    /// </summary>
    private static bool IsAvx2Supported() =>
#if NET7_0_OR_GREATER
        return global::System.Runtime.Intrinsics.X86.Avx2.IsSupported;
#else
        false;
#endif




    /// <summary>
    /// Checks if AVX-512 is supported on the current hardware.
    /// </summary>
    private static bool IsAvx512Supported() =>
#if NET8_0_OR_GREATER
        return global::System.Runtime.Intrinsics.X86.Avx512F.IsSupported;
#else
        false;
#endif




    /// <summary>
    /// Checks if SSE is supported on the current hardware.
    /// </summary>
    private static bool IsSseSupported() =>
#if NET7_0_OR_GREATER
        return global::System.Runtime.Intrinsics.X86.Sse.IsSupported;
#else
        false;
#endif




    /// <summary>
    /// Generates a vectorized loop template.
    /// </summary>
    /// <param name="elementType">The element type.</param>
    /// <param name="vectorSize">Vector size in bytes.</param>
    /// <param name="operation">The operation to perform.</param>
    /// <returns>Vectorized loop template code.</returns>
    public static string GenerateVectorizedLoopTemplate(string elementType, int vectorSize, string operation)
    {
        var vectorType = GetSimdType(elementType, vectorSize);
        var elementCount = GetVectorElementCount(elementType, vectorSize);
        var intrinsic = GetIntrinsicOperation(operation, vectorType);


        return $@"
// Vectorized loop using {vectorType}
var vectorSize = {elementCount};
var remainder = length % vectorSize;
var alignedLength = length - remainder;

// Process aligned data
for (int i = 0; i < alignedLength; i += vectorSize)
{{
    var va = {vectorType}.Load(a, i);
    var vb = {vectorType}.Load(b, i);
    var result = {intrinsic}(va, vb);
    result.Store(output, i);
}}

// Process remainder
for (int i = alignedLength; i < length; i++)
{{
    output[i] = a[i] {operation} b[i];
}}";
    }
}
