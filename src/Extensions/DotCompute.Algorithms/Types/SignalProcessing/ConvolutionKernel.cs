// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Types.SignalProcessing
{

/// <summary>
/// Represents a convolution kernel for signal processing operations.
/// </summary>
public sealed class ConvolutionKernel
{
    public required float[] Coefficients { get; init; }
    public required int Size { get; init; }
    public string? Name { get; init; }
}

/// <summary>
/// Configuration for convolution operations.
/// </summary>
public sealed class ConvolutionConfig
{
    public required int InputSize { get; init; }
    public required int KernelSize { get; init; }
    public ConvolutionMode Mode { get; init; } = ConvolutionMode.Valid;
}

/// <summary>
/// Convolution operation modes.
/// </summary>
public enum ConvolutionMode
{
    Valid,
    Same,
    Full
}}
