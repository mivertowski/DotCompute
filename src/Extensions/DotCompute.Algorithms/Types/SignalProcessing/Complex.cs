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
public readonly struct Complex(float real, float imaginary)
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
}