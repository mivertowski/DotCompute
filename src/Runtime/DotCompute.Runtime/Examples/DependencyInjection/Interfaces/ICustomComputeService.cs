// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Runtime.Examples.DependencyInjection.Interfaces;

/// <summary>
/// Defines the contract for custom compute services that leverage DotCompute through dependency injection.
/// This interface provides a foundation for implementing compute-intensive operations with proper DI integration.
/// </summary>
/// <remarks>
/// Implementations of this interface should:
/// - Use injected services like <see cref="IAcceleratorManager"/> and <see cref="IMemoryPoolService"/>
/// - Handle computation errors gracefully
/// - Log operations appropriately
/// - Follow async patterns for non-blocking operations
/// </remarks>
public interface ICustomComputeService
{
    /// <summary>
    /// Performs a custom computation operation using available accelerators and memory pools.
    /// </summary>
    /// <returns>A task representing the asynchronous computation operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the service is not properly initialized.</exception>
    /// <exception cref="ComputeException">Thrown when computation fails due to accelerator or memory issues.</exception>
    Task PerformComputationAsync();
}