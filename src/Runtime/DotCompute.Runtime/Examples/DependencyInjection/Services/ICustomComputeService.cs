// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Runtime.Examples.DependencyInjection.Services;

/// <summary>
/// Interface for custom compute service that demonstrates DI integration with DotCompute
/// </summary>
public interface ICustomComputeService
{
    /// <summary>
    /// Performs a computation using DotCompute runtime services
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    Task PerformComputationAsync();
}