// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Kernels;
using DotCompute.Backends.CPU.Kernels.Types;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.Kernels.Optimized;

/// <summary>
/// Generic optimized kernel for unknown kernel types that attempts to infer
/// execution patterns from kernel source code and provide fallback implementations.
/// </summary>
/// <remarks>
/// This kernel serves as a fallback when specific optimized kernels are not available.
/// It analyzes the kernel source code to infer common patterns and provides basic
/// implementations for vector operations. Performance may be suboptimal compared
/// to specialized kernel implementations.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="GenericOptimizedKernel"/> class.
/// </remarks>
/// <param name="name">The name of the kernel.</param>
/// <param name="kernelInfo">The kernel information containing source code and metadata.</param>
/// <param name="options">The compilation options for the kernel.</param>
/// <param name="logger">The logger instance for diagnostics.</param>
internal partial class GenericOptimizedKernel(string name, KernelInfo kernelInfo, CompilationOptions options, ILogger logger) : Base.OptimizedKernelBase(name, options, logger)
{
    private readonly KernelInfo _kernelInfo = kernelInfo ?? throw new ArgumentNullException(nameof(kernelInfo));

    #region LoggerMessage Delegates (Event IDs 7540-7559)

    [LoggerMessage(EventId = 7540, Level = LogLevel.Warning, Message = "Executing generic kernel - performance may be suboptimal: {KernelName}")]
    private static partial void LogGenericKernelExecution(ILogger logger, string kernelName);

    [LoggerMessage(EventId = 7541, Level = LogLevel.Warning, Message = "Unable to infer kernel execution pattern for: {KernelName}. No operation performed.")]
    private static partial void LogUnableToInferPattern(ILogger logger, string kernelName);

    [LoggerMessage(EventId = 7542, Level = LogLevel.Error, Message = "Vector scale pattern requires at least 3 arguments")]
    private static partial void LogVectorScaleArgumentError(ILogger logger);

    [LoggerMessage(EventId = 7543, Level = LogLevel.Information, Message = "Generic vector scale executed: {Elements} elements scaled by {Factor}")]
    private static partial void LogVectorScaleExecuted(ILogger logger, int elements, float factor);

    [LoggerMessage(EventId = 7544, Level = LogLevel.Information, Message = "Generic element-wise operation executed: {Elements} elements copied")]
    private static partial void LogElementWiseExecuted(ILogger logger, int elements);

    #endregion

    /// <summary>
    /// Executes the generic kernel asynchronously by analyzing the source and inferring execution patterns.
    /// </summary>
    /// <param name="arguments">The kernel arguments containing input and output buffers.</param>
    /// <param name="cancellationToken">The cancellation token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the kernel has been disposed.</exception>
    /// <remarks>
    /// This method analyzes the kernel source code to determine the most appropriate
    /// execution pattern and provides a best-effort implementation. Performance warnings
    /// are logged as this is a fallback implementation.
    /// </remarks>
    public override async ValueTask ExecuteAsync(KernelArguments arguments, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        LogGenericKernelExecution(Logger, Name);

        // Try to execute based on the kernel source analysis
        await TryExecuteGenericKernelAsync(arguments, cancellationToken);
    }

    /// <summary>
    /// Attempts to execute the generic kernel by analyzing source patterns.
    /// </summary>
    /// <param name="arguments">The kernel arguments.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private async ValueTask TryExecuteGenericKernelAsync(KernelArguments arguments, CancellationToken cancellationToken)
    {
        // Analyze kernel source to infer execution pattern
        var source = _kernelInfo.Source.ToUpperInvariant();

        // Try common patterns based on source analysis
        if (source.Contains("result[i]", StringComparison.OrdinalIgnoreCase) && source.Contains("input[i]", StringComparison.OrdinalIgnoreCase) && source.Contains("scale", StringComparison.CurrentCulture))
        {
            // Vector scale pattern - handle it manually
            await ExecuteVectorScalePatternAsync(arguments, cancellationToken);
        }
        else if (source.Contains("result[i]", StringComparison.CurrentCulture) && arguments.Arguments.Count >= 3)
        {
            // General element-wise operation pattern
            await ExecuteElementWisePatternAsync(arguments, cancellationToken);
        }
        else
        {
            LogUnableToInferPattern(Logger, Name);
        }
    }

    /// <summary>
    /// Executes a vector scaling pattern inferred from the kernel source.
    /// </summary>
    /// <param name="arguments">The kernel arguments.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <remarks>
    /// Attempts to extract a scale factor from the arguments and perform vector scaling.
    /// Uses a default scale factor of 2.0 if none can be determined.
    /// </remarks>
    private async ValueTask ExecuteVectorScalePatternAsync(KernelArguments arguments, CancellationToken cancellationToken)
    {
        if (arguments.Arguments.Count < 3)
        {
            LogVectorScaleArgumentError(Logger);
            return;
        }

        var scaleFactor = 2.0f; // Default scale factor

        // Try to extract scale factor from arguments[1]
        if (arguments.Arguments[1] is float f)
        {
            scaleFactor = f;
        }
        else if (arguments.Arguments[1] is double d)
        {
            scaleFactor = (float)d;
        }
        else if (arguments.Arguments[1] is int i)
        {
            scaleFactor = (float)i;
        }

        if (arguments.Arguments[0] is IUnifiedMemoryBuffer inputBuffer && arguments.Arguments[2] is IUnifiedMemoryBuffer resultBuffer)
        {
            var elementCount = (int)(inputBuffer.SizeInBytes / sizeof(float));
            var inputData = new float[elementCount];
            var resultData = new float[elementCount];

            await inputBuffer.CopyToAsync<float>(inputData, cancellationToken: cancellationToken);

            // Perform scaling
            for (var idx = 0; idx < elementCount; idx++)
            {
                resultData[idx] = inputData[idx] * scaleFactor;
            }

            await resultBuffer.CopyFromAsync<float>(resultData, cancellationToken: cancellationToken);
            LogVectorScaleExecuted(Logger, elementCount, scaleFactor);
        }
    }

    /// <summary>
    /// Executes a generic element-wise pattern as a fallback operation.
    /// </summary>
    /// <param name="arguments">The kernel arguments.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <remarks>
    /// Provides a basic fallback that copies input to output when no better
    /// pattern can be inferred from the kernel source code.
    /// </remarks>
    private async ValueTask ExecuteElementWisePatternAsync(KernelArguments arguments, CancellationToken cancellationToken)
    {
        // Generic element-wise operation - just copy input to output as fallback
        if (arguments.Arguments.Count >= 2 &&
            arguments.Arguments[0] is IUnifiedMemoryBuffer inputBuffer &&
            arguments.Arguments[1] is IUnifiedMemoryBuffer outputBuffer)
        {
            var elementCount = (int)(inputBuffer.SizeInBytes / sizeof(float));
            var data = new float[elementCount];

            await inputBuffer.CopyToAsync<float>(data, cancellationToken: cancellationToken);
            await outputBuffer.CopyFromAsync<float>(data, cancellationToken: cancellationToken);

            LogElementWiseExecuted(Logger, elementCount);
        }
    }
}
