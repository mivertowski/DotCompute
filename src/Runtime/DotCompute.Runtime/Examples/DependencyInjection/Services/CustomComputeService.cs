// <copyright file="CustomComputeService.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using DotCompute.Runtime.Examples.DependencyInjection.Interfaces;
using DotCompute.Runtime.Services;
using Microsoft.Extensions.Logging;

namespace DotCompute.Runtime.Examples.DependencyInjection.Services;

/// <summary>
/// Example implementation of a custom compute service using dependency injection.
/// Demonstrates integration with DotCompute services through constructor injection.
/// </summary>
public class CustomComputeService : ICustomComputeService
{
    private readonly IAcceleratorManager _acceleratorManager;
    private readonly IMemoryPoolService _memoryPool;
    private readonly ILogger<CustomComputeService> _logger;

    /// <summary>
    /// Initializes a new instance of the CustomComputeService class.
    /// </summary>
    /// <param name="acceleratorManager">The accelerator manager service.</param>
    /// <param name="memoryPool">The memory pool service.</param>
    /// <param name="logger">The logger for this service.</param>
    public CustomComputeService(
        IAcceleratorManager acceleratorManager,
        IMemoryPoolService memoryPool,
        ILogger<CustomComputeService> logger)
    {
        _acceleratorManager = acceleratorManager;
        _memoryPool = memoryPool;
        _logger = logger;
    }

    /// <summary>
    /// Performs a custom computation using injected DotCompute services.
    /// </summary>
    /// <returns>A task representing the asynchronous computation operation.</returns>
    public async Task PerformComputationAsync()
    {
        _logger.LogInformation("Starting custom computation");

        var accelerator = _acceleratorManager.Default;
        var pool = _memoryPool.GetPool(accelerator.Info.Id);

        using var buffer = await pool.AllocateAsync(1024 * 1024); // 1MB

        _logger.LogInformation("Allocated {SizeKB}KB from memory pool", buffer.SizeInBytes / 1024);

        // Simulate computation
        await accelerator.SynchronizeAsync();

        _logger.LogInformation("Custom computation completed");
    }
}