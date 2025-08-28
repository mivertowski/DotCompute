// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using DotCompute.Abstractions.Types;

namespace DotCompute.Abstractions.Validation;

/// <summary>
/// Validation result specifically for accelerator configurations and capabilities.
/// This extends UnifiedValidationResult with accelerator-specific properties.
/// </summary>
public sealed class AcceleratorValidationResult
{
    private readonly UnifiedValidationResult _baseResult;

    /// <summary>
    /// Gets whether the accelerator configuration is valid.
    /// </summary>
    public bool IsValid => _baseResult.IsValid;

    /// <summary>
    /// Gets whether there are validation warnings.
    /// </summary>
    public bool HasWarnings => _baseResult.HasWarnings;

    /// <summary>
    /// Gets the first error message if any.
    /// </summary>
    public string? ErrorMessage => _baseResult.ErrorMessage;

    /// <summary>
    /// Gets all validation errors.
    /// </summary>
    public IReadOnlyList<ValidationIssue> Errors => _baseResult.Errors;

    /// <summary>
    /// Gets all validation warnings.
    /// </summary>
    public IReadOnlyList<ValidationIssue> Warnings => _baseResult.Warnings;

    /// <summary>
    /// Gets all informational messages.
    /// </summary>
    public IReadOnlyList<ValidationIssue> Information => _baseResult.Information;

    /// <summary>
    /// Gets the validated supported features if validation succeeded.
    /// </summary>
    public IReadOnlyList<string> SupportedFeatures { get; }

    /// <summary>
    /// Gets the performance metrics collected during validation.
    /// </summary>
    public AcceleratorPerformanceMetrics? PerformanceMetrics { get; }

    /// <summary>
    /// Gets the accelerator type that was validated.
    /// </summary>
    public AcceleratorType AcceleratorType { get; }

    /// <summary>
    /// Gets the device index that was validated.
    /// </summary>
    public int DeviceIndex { get; }

    /// <summary>
    /// Gets the validation timestamp.
    /// </summary>
    public DateTimeOffset Timestamp => _baseResult.Timestamp;

    /// <summary>
    /// Initializes a new instance of AcceleratorValidationResult.
    /// </summary>
    private AcceleratorValidationResult(
        UnifiedValidationResult baseResult,
        AcceleratorType acceleratorType,
        int deviceIndex,
        IReadOnlyList<string>? supportedFeatures = null,
        AcceleratorPerformanceMetrics? performanceMetrics = null)
    {
        _baseResult = baseResult ?? throw new ArgumentNullException(nameof(baseResult));
        AcceleratorType = acceleratorType;
        DeviceIndex = deviceIndex;
        SupportedFeatures = supportedFeatures ?? Array.Empty<string>();
        PerformanceMetrics = performanceMetrics;
    }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <param name="acceleratorType">The validated accelerator type.</param>
    /// <param name="deviceIndex">The validated device index.</param>
    /// <param name="supportedFeatures">List of supported features.</param>
    /// <param name="performanceMetrics">Performance metrics collected during validation.</param>
    /// <returns>A successful validation result.</returns>
    public static AcceleratorValidationResult Success(
        AcceleratorType acceleratorType,
        int deviceIndex = 0,
        IReadOnlyList<string>? supportedFeatures = null,
        AcceleratorPerformanceMetrics? performanceMetrics = null)
    {
        return new AcceleratorValidationResult(
            UnifiedValidationResult.Success(),
            acceleratorType,
            deviceIndex,
            supportedFeatures,
            performanceMetrics);
    }

    /// <summary>
    /// Creates a successful validation result with features and metrics.
    /// </summary>
    /// <param name="supportedFeatures">List of supported features.</param>
    /// <param name="performanceMetrics">Performance metrics collected during validation.</param>
    /// <returns>A successful validation result.</returns>
    public static AcceleratorValidationResult Success(
        IReadOnlyList<string> supportedFeatures,
        AcceleratorPerformanceMetrics performanceMetrics)
    {
        return new AcceleratorValidationResult(
            UnifiedValidationResult.Success(),
            AcceleratorType.Auto, // Will be determined by the factory
            0,
            supportedFeatures,
            performanceMetrics);
    }

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    /// <param name="errors">Validation errors.</param>
    /// <param name="warnings">Optional validation warnings.</param>
    /// <param name="acceleratorType">The accelerator type being validated.</param>
    /// <param name="deviceIndex">The device index being validated.</param>
    /// <returns>A failed validation result.</returns>
    public static AcceleratorValidationResult Failure(
        IEnumerable<string> errors,
        IEnumerable<string>? warnings = null,
        AcceleratorType acceleratorType = AcceleratorType.Auto,
        int deviceIndex = 0)
    {
        var result = new UnifiedValidationResult();
        
        foreach (var error in errors)
        {
            result.AddError(error, "ACCELERATOR_VALIDATION", "AcceleratorFactory");
        }


        if (warnings != null)
        {
            foreach (var warning in warnings)
            {
                result.AddWarning(warning, "ACCELERATOR_VALIDATION", "AcceleratorFactory");
            }

        }

        return new AcceleratorValidationResult(result, acceleratorType, deviceIndex);
    }

    /// <summary>
    /// Creates a failed validation result from a single error.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="acceleratorType">The accelerator type being validated.</param>
    /// <param name="deviceIndex">The device index being validated.</param>
    /// <returns>A failed validation result.</returns>
    public static AcceleratorValidationResult Failure(
        string errorMessage,
        AcceleratorType acceleratorType = AcceleratorType.Auto,
        int deviceIndex = 0) => Failure(new[] { errorMessage }, null, acceleratorType, deviceIndex);

    /// <summary>
    /// Creates a validation result from an exception.
    /// </summary>
    /// <param name="exception">The exception that occurred during validation.</param>
    /// <param name="acceleratorType">The accelerator type being validated.</param>
    /// <param name="deviceIndex">The device index being validated.</param>
    /// <returns>A failed validation result.</returns>
    public static AcceleratorValidationResult FromException(
        Exception exception,
        AcceleratorType acceleratorType = AcceleratorType.Auto,
        int deviceIndex = 0)
    {
        var baseResult = UnifiedValidationResult.FromException(exception);
        return new AcceleratorValidationResult(baseResult, acceleratorType, deviceIndex);
    }

    /// <summary>
    /// Throws an exception if validation failed.
    /// </summary>
    /// <exception cref="AcceleratorValidationException">Thrown when validation fails.</exception>
    public void ThrowIfInvalid()
    {
        if (!IsValid)
        {
            throw new AcceleratorValidationException(this);
        }
    }

    /// <summary>
    /// Gets a summary message of all errors.
    /// </summary>
    /// <returns>A formatted error summary.</returns>
    public string GetErrorSummary() => _baseResult.GetErrorSummary();

    /// <summary>
    /// Gets a full summary of all validation issues.
    /// </summary>
    /// <returns>A formatted full summary.</returns>
    public string GetFullSummary()
    {
        var summary = _baseResult.GetFullSummary();
        var details = new List<string>();

        if (!string.IsNullOrEmpty(summary))
        {
            details.Add(summary);
        }


        details.Add($"Accelerator Type: {AcceleratorType}");
        details.Add($"Device Index: {DeviceIndex}");

        if (SupportedFeatures.Any())
        {
            details.Add($"Supported Features: {string.Join(", ", SupportedFeatures)}");
        }


        if (PerformanceMetrics != null)
        {

            details.Add($"Performance Metrics: {PerformanceMetrics}");
        }


        return string.Join(Environment.NewLine, details);
    }

    /// <summary>
    /// Converts to the underlying UnifiedValidationResult.
    /// </summary>
    /// <returns>The underlying UnifiedValidationResult.</returns>
    public UnifiedValidationResult ToUnifiedResult() => _baseResult;

    /// <summary>
    /// Implicit conversion to UnifiedValidationResult.
    /// </summary>
    /// <param name="result">The AcceleratorValidationResult to convert.</param>
    /// <returns>The underlying UnifiedValidationResult.</returns>
    public static implicit operator UnifiedValidationResult(AcceleratorValidationResult result)
        => result._baseResult;
}

/// <summary>
/// Performance metrics collected during accelerator validation.
/// </summary>
public sealed class AcceleratorPerformanceMetrics
{
    /// <summary>
    /// Gets the memory bandwidth in GB/s.
    /// </summary>
    public double MemoryBandwidthGBps { get; init; }

    /// <summary>
    /// Gets the compute capability score.
    /// </summary>
    public double ComputeCapabilityScore { get; init; }

    /// <summary>
    /// Gets the initialization time in milliseconds.
    /// </summary>
    public double InitializationTimeMs { get; init; }

    /// <summary>
    /// Gets the device memory size in bytes.
    /// </summary>
    public long DeviceMemoryBytes { get; init; }

    /// <summary>
    /// Gets the maximum work group size.
    /// </summary>
    public int MaxWorkGroupSize { get; init; }

    /// <summary>
    /// Gets whether the device supports unified memory.
    /// </summary>
    public bool SupportsUnifiedMemory { get; init; }

    /// <summary>
    /// Gets additional custom metrics.
    /// </summary>
    public IReadOnlyDictionary<string, object> CustomMetrics { get; init; } = 
        new Dictionary<string, object>();

    /// <summary>
    /// Returns a string representation of the performance metrics.
    /// </summary>
    /// <returns>A formatted string with key performance metrics.</returns>
    public override string ToString()
    {
        return $"Memory: {MemoryBandwidthGBps:F1} GB/s, " +
               $"Compute: {ComputeCapabilityScore:F1}, " +
               $"Init: {InitializationTimeMs:F1}ms, " +
               $"Device Memory: {DeviceMemoryBytes / (1024.0 * 1024 * 1024):F1} GB";
    }
}

/// <summary>
/// Exception thrown when accelerator validation fails.
/// </summary>
public sealed class AcceleratorValidationException : Exception
{
    /// <summary>
    /// Gets the validation result that caused this exception.
    /// </summary>
    public AcceleratorValidationResult ValidationResult { get; }

    /// <summary>
    /// Initializes a new instance of AcceleratorValidationException.
    /// </summary>
    /// <param name="validationResult">The validation result that failed.</param>
    public AcceleratorValidationException(AcceleratorValidationResult validationResult)
        : base(validationResult?.GetErrorSummary() ?? "Accelerator validation failed")
    {
        ValidationResult = validationResult ?? throw new ArgumentNullException(nameof(validationResult));
    }

    /// <summary>
    /// Initializes a new instance of AcceleratorValidationException with a custom message.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public AcceleratorValidationException(string message)
        : base(message)
    {
        ValidationResult = AcceleratorValidationResult.Failure(message);
    }

    /// <summary>
    /// Initializes a new instance of AcceleratorValidationException with a custom message and inner exception.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public AcceleratorValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
        ValidationResult = AcceleratorValidationResult.Failure(message);
    }
    public AcceleratorValidationException()
    {
        ValidationResult = AcceleratorValidationResult.Failure("Unknown accelerator validation error");
    }

}