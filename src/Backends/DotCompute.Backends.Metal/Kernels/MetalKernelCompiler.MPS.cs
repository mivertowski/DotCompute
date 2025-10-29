// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Abstractions.Kernels.Types;
using DotCompute.Backends.Metal.MPS;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.Metal.Kernels;

/// <summary>
/// MPS integration extension for MetalKernelCompiler.
/// Provides intelligent fallback to Metal Performance Shaders for standard operations.
/// </summary>
public sealed partial class MetalKernelCompiler
{
    private MetalMPSDetector? _mpsDetector;

    /// <summary>
    /// Initializes MPS detection capabilities if available on the device.
    /// </summary>
    private void InitializeMPSDetection()
    {
        try
        {
            var mpsCapabilities = MetalMPSNative.QueryMPSCapabilities(_device);
            _mpsDetector = new MetalMPSDetector(_logger, mpsCapabilities);
            _logger.LogInformation(
                "MPS detector initialized - BLAS: {BLAS}, CNN: {CNN}, Neural: {Neural}, GPU: {GPUFamily}",
                mpsCapabilities.HasBLAS,
                mpsCapabilities.HasCNN,
                mpsCapabilities.HasNeuralNetwork,
                mpsCapabilities.GPUFamily);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MPS detector initialization failed - MPS acceleration disabled");
            _mpsDetector = null;
        }
    }

    /// <summary>
    /// Attempts to use MPS for kernel compilation if the operation is supported.
    /// Returns null if MPS cannot be used, otherwise returns an MPS-optimized kernel.
    /// </summary>
    private ICompiledKernel? TryCompileWithMPS(
        KernelDefinition definition,
        CompilationOptions? options)
    {
        if (_mpsDetector == null)
        {
            return null;
        }

        if (!_mpsDetector.CanUseMPS(definition, out var mpsOperation))
        {
            _logger.LogTrace("MPS not applicable for kernel '{Name}'", definition.Name);
            return null;
        }

        _logger.LogInformation(
            "Using Metal Performance Shaders for kernel '{Name}' - {Operation} (Expected 3-10x speedup)",
            definition.Name,
            mpsOperation);

        var mpsMetadata = new CompilationMetadata
        {
            CompilationTimeMs = 0, // No compilation for MPS
            MemoryUsage =
            {
                ["Backend"] = "MPS",
                ["Operation"] = mpsOperation.ToString(),
                ["ExpectedSpeedup"] = GetExpectedSpeedup(mpsOperation)
            },
            Warnings =
            {
                $"Using MPS for {mpsOperation}",
                "Optimized performance with Metal Performance Shaders",
                $"Expected speedup: {GetExpectedSpeedup(mpsOperation)}"
            }
        };

        return new MetalMPSKernel(_device, mpsOperation, definition.Name, _logger);
    }

    /// <summary>
    /// Gets the expected performance speedup factor for an MPS operation.
    /// </summary>
    private static string GetExpectedSpeedup(MPSOperationType operation)
    {
        return operation switch
        {
            MPSOperationType.MatrixMultiplication => "3-4x",
            MPSOperationType.MatrixVectorMultiplication => "3-4x",
            MPSOperationType.Convolution2D => "3-5x",
            MPSOperationType.MaxPooling2D => "2-3x",
            MPSOperationType.ReLU => "2-4x",
            MPSOperationType.Sigmoid => "2-4x",
            MPSOperationType.Tanh => "2-4x",
            MPSOperationType.BatchNormalization => "2-4x",
            MPSOperationType.ElementWiseAdd => "2-3x",
            MPSOperationType.ElementWiseMultiply => "2-3x",
            MPSOperationType.ReduceSum => "2-3x",
            MPSOperationType.ReduceMax => "2-3x",
            MPSOperationType.ReduceMin => "2-3x",
            MPSOperationType.ImageConversion => "2-4x",
            MPSOperationType.GaussianBlur => "2-4x",
            _ => "1-2x"
        };
    }
}
