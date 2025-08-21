// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Abstractions.Accelerators;
using Microsoft.Extensions.DependencyInjection;

namespace DotCompute.Runtime.Factories;


/// <summary>
/// Factory for creating accelerator instances with dependency injection support
/// </summary>
public interface IAcceleratorFactory
{
    /// <summary>
    /// Creates an accelerator instance with full DI support
    /// </summary>
    /// <param name="acceleratorInfo">The accelerator information</param>
    /// <param name="serviceProvider">The service provider</param>
    /// <returns>The created accelerator instance</returns>
    public Task<IAccelerator> CreateAcceleratorAsync(AcceleratorInfo acceleratorInfo, IServiceProvider serviceProvider);

    /// <summary>
    /// Creates an accelerator provider with DI support
    /// </summary>
    /// <typeparam name="TProvider">The provider type</typeparam>
    /// <param name="serviceProvider">The service provider</param>
    /// <returns>The created accelerator provider</returns>
    public Task<TProvider> CreateProviderAsync<TProvider>(IServiceProvider serviceProvider) where TProvider : class, IAcceleratorProvider;

    /// <summary>
    /// Checks if an accelerator type can be created
    /// </summary>
    /// <param name="acceleratorType">The accelerator type</param>
    /// <returns>True if the accelerator can be created</returns>
    public bool CanCreateAccelerator(AcceleratorType acceleratorType);

    /// <summary>
    /// Gets the supported accelerator types
    /// </summary>
    /// <returns>Supported accelerator types</returns>
    public IEnumerable<AcceleratorType> GetSupportedTypes();

    /// <summary>
    /// Registers a custom accelerator provider type
    /// </summary>
    /// <param name="providerType">The provider type</param>
    /// <param name="supportedTypes">The accelerator types supported by this provider</param>
    public void RegisterProvider(Type providerType, params AcceleratorType[] supportedTypes);

    /// <summary>
    /// Creates a service scope for accelerator operations
    /// </summary>
    /// <param name="acceleratorId">The accelerator identifier</param>
    /// <returns>A service scope for the accelerator</returns>
    public IServiceScope CreateAcceleratorScope(string acceleratorId);
}

/// <summary>
/// Configuration for accelerator creation
/// </summary>
public class AcceleratorCreationOptions
{
    /// <summary>
    /// Gets or sets whether to enable debug mode
    /// </summary>
    public bool EnableDebugMode { get; set; }

    /// <summary>
    /// Gets or sets the maximum memory allocation size
    /// </summary>
    public long? MaxMemoryAllocationSize { get; set; }

    /// <summary>
    /// Gets or sets whether to enable profiling
    /// </summary>
    public bool EnableProfiling { get; set; }

    /// <summary>
    /// Gets or sets custom properties for the accelerator
    /// </summary>
    public Dictionary<string, object> CustomProperties { get; set; } = [];

    /// <summary>
    /// Gets or sets the service lifetime for this accelerator
    /// </summary>
    public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Singleton;

    /// <summary>
    /// Gets or sets whether to validate the accelerator after creation
    /// </summary>
    public bool ValidateAfterCreation { get; set; } = true;
}

/// <summary>
/// Result of accelerator creation
/// </summary>
public class AcceleratorCreationResult
{
    /// <summary>
    /// Gets the created accelerator instance
    /// </summary>
    public required IAccelerator Accelerator { get; init; }

    /// <summary>
    /// Gets whether the creation was successful
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets any warnings that occurred during creation
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the validation results if validation was performed
    /// </summary>
    public AcceleratorValidationResult? ValidationResult { get; init; }

    /// <summary>
    /// Gets the time taken to create the accelerator
    /// </summary>
    public TimeSpan CreationTime { get; init; }

    /// <summary>
    /// Gets additional metadata about the creation process
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = [];
}

/// <summary>
/// Result of accelerator validation
/// </summary>
public class AcceleratorValidationResult
{
    /// <summary>
    /// Gets whether the validation was successful
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the validation errors
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the validation warnings
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the supported features detected
    /// </summary>
    public AcceleratorFeature SupportedFeatures { get; init; }

    /// <summary>
    /// Gets the performance metrics from validation
    /// </summary>
    public Dictionary<string, double> PerformanceMetrics { get; init; } = [];

    /// <summary>
    /// Creates a successful validation result
    /// </summary>
    /// <param name="supportedFeatures">Supported features</param>
    /// <param name="performanceMetrics">Performance metrics</param>
    /// <returns>A successful validation result</returns>
    public static AcceleratorValidationResult Success(
        AcceleratorFeature supportedFeatures = AcceleratorFeature.None,
        Dictionary<string, double>? performanceMetrics = null)
        => new()
        {
            IsValid = true,
            SupportedFeatures = supportedFeatures,
            PerformanceMetrics = performanceMetrics ?? []
        };

    /// <summary>
    /// Creates a failed validation result
    /// </summary>
    /// <param name="errors">Validation errors</param>
    /// <param name="warnings">Validation warnings</param>
    /// <returns>A failed validation result</returns>
    public static AcceleratorValidationResult Failure(
        IEnumerable<string> errors,
        IEnumerable<string>? warnings = null)
        => new()
        {
            IsValid = false,
            Errors = errors.ToList(),
            Warnings = warnings?.ToList() ?? []
        };
}
