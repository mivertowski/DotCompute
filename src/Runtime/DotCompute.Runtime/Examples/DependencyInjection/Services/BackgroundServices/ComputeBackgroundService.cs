// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Runtime.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotCompute.Runtime.Examples.DependencyInjection.Services.BackgroundServices;

/// <summary>
/// Example background service using DotCompute
/// </summary>
public class ComputeBackgroundService : BackgroundService
{
    private readonly AcceleratorRuntime _runtime;
    private readonly ILogger<ComputeBackgroundService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComputeBackgroundService"/> class
    /// </summary>
    /// <param name="runtime">The accelerator runtime</param>
    /// <param name="logger">The logger</param>
    public ComputeBackgroundService(
        AcceleratorRuntime runtime,
        ILogger<ComputeBackgroundService> logger)
    {
        _runtime = runtime;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Background compute service running at: {Time}", DateTimeOffset.Now);

            var accelerators = _runtime.GetAccelerators();
            _logger.LogInformation("Available accelerators: {Count}", accelerators.Count);

            await Task.Delay(5000, stoppingToken);
        }
    }
}