// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Runtime.Examples.DependencyInjection.Interfaces;

/// <summary>
/// Defines the contract for custom data providers that supply initialization data for compute operations.
/// This interface is commonly used by plugins and services that require external data sources.
/// </summary>
/// <remarks>
/// Implementations should:
/// - Handle data retrieval errors gracefully
/// - Support cancellation tokens for long-running operations
/// - Cache frequently requested data when appropriate
/// - Validate data integrity before returning
/// </remarks>
public interface ICustomDataProvider
{
    /// <summary>
    /// Retrieves initialization data required for compute operations.
    /// </summary>
    /// <returns>A task containing the initialization data as a byte array.</returns>
    /// <exception cref="DataProviderException">Thrown when data retrieval fails.</exception>
    /// <exception cref="InvalidDataException">Thrown when retrieved data is corrupted or invalid.</exception>
    Task<byte[]> GetInitializationDataAsync();
}