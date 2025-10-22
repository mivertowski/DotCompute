
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Types.SignalProcessing;


/// <summary>
/// Represents a convolution kernel for signal processing operations.
/// </summary>
public sealed class ConvolutionKernel
{
    /// <summary>
    /// Gets or sets the coefficients.
    /// </summary>
    /// <value>The coefficients.</value>
    public required IReadOnlyList<float> Coefficients { get; init; }
    /// <summary>
    /// Gets or sets the size.
    /// </summary>
    /// <value>The size.</value>
    public required int Size { get; init; }
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <value>The name.</value>
    public string? Name { get; init; }
}

/// <summary>
/// Configuration for convolution operations.
/// </summary>
public sealed class ConvolutionConfig
{
    /// <summary>
    /// Gets or sets the input size.
    /// </summary>
    /// <value>The input size.</value>
    public required int InputSize { get; init; }
    /// <summary>
    /// Gets or sets the kernel size.
    /// </summary>
    /// <value>The kernel size.</value>
    public required int KernelSize { get; init; }
    /// <summary>
    /// Gets or sets the mode.
    /// </summary>
    /// <value>The mode.</value>
    public ConvolutionMode Mode { get; init; } = ConvolutionMode.Valid;
}
/// <summary>
/// An convolution mode enumeration.
/// </summary>

/// <summary>
/// Convolution operation modes.
/// </summary>
public enum ConvolutionMode
{
    Valid,
    Same,
    Full
}
