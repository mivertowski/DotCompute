// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Runtime.Examples.DependencyInjection.Mocks;
using DotCompute.Runtime.Factories;
using Microsoft.Extensions.Logging;

namespace DotCompute.Runtime.Examples.DependencyInjection.Providers;

/// <summary>
/// Example custom accelerator provider
/// </summary>
public class CustomAcceleratorProvider : IAcceleratorProvider
{
    private readonly ILogger<CustomAcceleratorProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomAcceleratorProvider"/> class
    /// </summary>
    /// <param name="logger">The logger</param>
    public CustomAcceleratorProvider(ILogger<CustomAcceleratorProvider> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Custom";

    /// <inheritdoc />
    public AcceleratorType[] SupportedTypes => new[] { AcceleratorType.GPU };

    /// <inheritdoc />
    public ValueTask<IEnumerable<IAccelerator>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Discovering custom accelerators");

        // Return empty collection for this example
        return ValueTask.FromResult(Enumerable.Empty<IAccelerator>());
    }

    /// <inheritdoc />
    public ValueTask<IAccelerator> CreateAsync(AcceleratorInfo info, CancellationToken cancellationToken = default)
    {
        // For this example, we'll create a simple mock accelerator
        var mockAccelerator = new ExampleMockAccelerator(info);
        return ValueTask.FromResult<IAccelerator>(mockAccelerator);
    }
}