// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// This is a redirect to the main Complex implementation
// Re-export the Complex type for compatibility
namespace DotCompute.Algorithms.Types.SignalProcessing;

/// <summary>
/// Complex number type alias for compatibility.
/// </summary>
/// <remarks>
/// Initializes a new instance of the Complex struct.
/// </remarks>
/// <param name="real">The real component.</param>
/// <param name="imaginary">The imaginary component.</param>
public readonly struct Complex(float real, float imaginary) : IEquatable<Complex>
{
    private readonly Algorithms.SignalProcessing.Complex _inner = new(real, imaginary);

    /// <summary>
    /// Gets the real component.
    /// </summary>
    public float Real => _inner.Real;

    /// <summary>
    /// Gets the imaginary component.
    /// </summary>
    public float Imaginary => _inner.Imaginary;

    /// <summary>
    /// Gets the magnitude.
    /// </summary>
    public float Magnitude => _inner.Magnitude;

    /// <summary>
    /// Gets the phase.
    /// </summary>
    public float Phase => _inner.Phase;

    /// <summary>
    /// Implicit conversion from the main Complex type.
    /// </summary>
    public static implicit operator Complex(Algorithms.SignalProcessing.Complex complex)
    {
        return new Complex(complex.Real, complex.Imaginary);
    }

    /// <summary>
    /// Implicit conversion to the main Complex type.
    /// </summary>
    public static implicit operator Algorithms.SignalProcessing.Complex(Complex complex)
    {
        return complex._inner;
    }

    /// <summary>
    /// Creates a Complex instance from the main Complex type (alternative to implicit conversion).
    /// </summary>
    /// <param name="complex">The main Complex instance to convert.</param>
    /// <returns>A Complex instance.</returns>
    public static Complex ToComplex(Algorithms.SignalProcessing.Complex complex)
    {
        return new Complex(complex.Real, complex.Imaginary);
    }

    /// <summary>
    /// Converts a Complex instance to the main Complex type (alternative to implicit conversion).
    /// </summary>
    /// <param name="complex">The Complex instance to convert.</param>
    /// <returns>The main Complex instance.</returns>
    public static Algorithms.SignalProcessing.Complex FromComplex(Complex complex)
    {
        return complex._inner;
    }

    /// <inheritdoc/>
    public bool Equals(Complex other) => Real == other.Real && Imaginary == other.Imaginary;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Complex other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Real, Imaginary);

    /// <summary>
    /// Equality operator.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>True if the operands are equal; otherwise, false.</returns>
    public static bool operator ==(Complex left, Complex right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    /// <param name="left">The left operand.</param>
    /// <param name="right">The right operand.</param>
    /// <returns>True if the operands are not equal; otherwise, false.</returns>
    public static bool operator !=(Complex left, Complex right)
    {
        return !left.Equals(right);
    }
}