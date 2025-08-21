// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Runtime.Examples.DependencyInjection.Providers;

/// <summary>
/// Interface for custom data providers that can supply initialization data
/// </summary>
public interface ICustomDataProvider
{
    /// <summary>
    /// Gets initialization data asynchronously
    /// </summary>
    /// <returns>A task containing the initialization data as a byte array</returns>
    Task<byte[]> GetInitializationDataAsync();
}