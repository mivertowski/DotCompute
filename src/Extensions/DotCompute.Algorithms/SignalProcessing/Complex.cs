// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.CompilerServices;

namespace DotCompute.Algorithms.SignalProcessing;


/// <summary>
/// Represents a complex number with single-precision floating-point real and imaginary components.
/// </summary>
public readonly struct Complex : IEquatable<Complex>
{
/// <summary>
/// Gets the real component of the complex number.
/// </summary>
public float Real { get; }

/// <summary>
/// Gets the imaginary component of the complex number.
/// </summary>
public float Imaginary { get; }

/// <summary>
/// Initializes a new instance of the <see cref="Complex"/> struct.
/// </summary>
/// <param name="real">The real component.</param>
/// <param name="imaginary">The imaginary component.</param>
public Complex(float real, float imaginary)
{
    Real = real;
    Imaginary = imaginary;
}

/// <summary>
/// Gets the magnitude (absolute value) of the complex number.
/// </summary>
public float Magnitude
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    get => MathF.Sqrt(Real * Real + Imaginary * Imaginary);
}

/// <summary>
/// Gets the phase angle in radians.
/// </summary>
public float Phase
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    get => MathF.Atan2(Imaginary, Real);
}

/// <summary>
/// Gets the complex conjugate.
/// </summary>
public Complex Conjugate
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    get => new Complex(Real, -Imaginary);
}

/// <summary>
/// Gets a complex number with zero real and imaginary parts.
/// </summary>
public static Complex Zero { get; } = new(0, 0);

/// <summary>
/// Gets a complex number with one real part and zero imaginary part.
/// </summary>
public static Complex One { get; } = new(1, 0);

/// <summary>
/// Gets the imaginary unit (i).
/// </summary>
public static Complex ImaginaryOne { get; } = new(0, 1);

/// <summary>
/// Creates a complex number from polar coordinates.
/// </summary>
/// <param name="magnitude">The magnitude.</param>
/// <param name="phase">The phase angle in radians.</param>
/// <returns>A complex number.</returns>
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static Complex FromPolar(float magnitude, float phase)
{
    return new Complex(
        magnitude * MathF.Cos(phase),
        magnitude * MathF.Sin(phase));
}

/// <summary>
/// Adds two complex numbers.
/// </summary>
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static Complex operator +(Complex left, Complex right)
{
    return new Complex(left.Real + right.Real, left.Imaginary + right.Imaginary);
}

/// <summary>
/// Subtracts two complex numbers.
/// </summary>
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static Complex operator -(Complex left, Complex right)
{
    return new Complex(left.Real - right.Real, left.Imaginary - right.Imaginary);
}

/// <summary>
/// Multiplies two complex numbers.
/// </summary>
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static Complex operator *(Complex left, Complex right)
{
    return new Complex(
        left.Real * right.Real - left.Imaginary * right.Imaginary,
        left.Real * right.Imaginary + left.Imaginary * right.Real);
}

/// <summary>
/// Divides two complex numbers.
/// </summary>
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static Complex operator /(Complex left, Complex right)
{
    var denominator = right.Real * right.Real + right.Imaginary * right.Imaginary;
    return new Complex(
        (left.Real * right.Real + left.Imaginary * right.Imaginary) / denominator,
        (left.Imaginary * right.Real - left.Real * right.Imaginary) / denominator);
}

/// <summary>
/// Negates a complex number.
/// </summary>
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static Complex operator -(Complex value)
{
    return new Complex(-value.Real, -value.Imaginary);
}

/// <summary>
/// Multiplies a complex number by a scalar.
/// </summary>
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static Complex operator *(Complex left, float right)
{
    return new Complex(left.Real * right, left.Imaginary * right);
}

/// <summary>
/// Multiplies a scalar by a complex number.
/// </summary>
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static Complex operator *(float left, Complex right)
{
    return new Complex(left * right.Real, left * right.Imaginary);
}

    /// <summary>
    /// Computes e^(i*theta).
    /// </summary>
    /// <param name="theta">The angle in radians.</param>
    /// <returns>The complex exponential.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Complex Exp(float theta) => new Complex(MathF.Cos(theta), MathF.Sin(theta));

    /// <summary>
    /// Adds two complex numbers.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Complex Add(Complex left, Complex right) => left + right;

    /// <summary>
    /// Subtracts two complex numbers.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Complex Subtract(Complex left, Complex right) => left - right;

    /// <summary>
    /// Multiplies two complex numbers.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Complex Multiply(Complex left, Complex right) => left * right;

    /// <summary>
    /// Multiplies a complex number by a scalar.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Complex Multiply(Complex left, float right) => left * right;

    /// <summary>
    /// Multiplies a scalar by a complex number.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Complex Multiply(float left, Complex right) => left * right;

    /// <summary>
    /// Divides two complex numbers.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Complex Divide(Complex left, Complex right) => left / right;

    /// <summary>
    /// Negates a complex number.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Complex Negate(Complex value) => -value;

    /// <inheritdoc/>
    public bool Equals(Complex other) => Real == other.Real && Imaginary == other.Imaginary;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Complex other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Real, Imaginary);

    /// <inheritdoc/>
    public override string ToString() => Imaginary >= 0 ? $"{Real} + {Imaginary}i" : $"{Real} - {-Imaginary}i";

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(Complex left, Complex right)
{
    return left.Equals(right);
}

/// <summary>
/// Inequality operator.
/// </summary>
public static bool operator !=(Complex left, Complex right)
{
    return !left.Equals(right);
}
}
