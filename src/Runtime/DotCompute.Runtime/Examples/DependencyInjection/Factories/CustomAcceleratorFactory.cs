// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using DotCompute.Runtime.Configuration;
using DotCompute.Runtime.Examples.DependencyInjection.Providers;
using DotCompute.Runtime.Factories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotCompute.Runtime.Examples.DependencyInjection.Factories;

/// <summary>
/// Example custom accelerator factory
/// </summary>
public class CustomAcceleratorFactory : DefaultAcceleratorFactory
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CustomAcceleratorFactory"/> class
    /// </summary>
    /// <param name="serviceProvider">The service provider</param>
    /// <param name="options">The DotCompute runtime options</param>
    /// <param name="logger">The logger</param>
    public CustomAcceleratorFactory(
        IServiceProvider serviceProvider,
        IOptions<DotComputeRuntimeOptions> options,
        ILogger<CustomAcceleratorFactory> logger)
        : base(serviceProvider, options, logger)
    {
        // Register additional custom providers
        RegisterProvider(typeof(CustomAcceleratorProvider), AcceleratorType.GPU);
    }
}