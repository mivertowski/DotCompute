// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DotCompute.Runtime.Logging;
using System.Reflection;

namespace DotCompute.Runtime.DependencyInjection.Scopes;

/// <summary>
/// Scoped service provider wrapper for plugins.
/// </summary>
internal sealed class PluginScopedServiceProvider(IServiceScope scope, Assembly pluginAssembly, ILogger logger) : IServiceProvider
{
    private readonly IServiceScope _scope = scope ?? throw new ArgumentNullException(nameof(scope));
    private readonly Assembly _pluginAssembly = pluginAssembly ?? throw new ArgumentNullException(nameof(pluginAssembly));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public object? GetService(Type serviceType)
    {
        try
        {
            var service = _scope.ServiceProvider.GetService(serviceType);
            if (service != null)
            {
                _logger.LogTrace("Retrieved service {ServiceType} for plugin assembly {AssemblyName}",
                    serviceType.Name, _pluginAssembly.GetName().Name);
            }
            return service;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Failed to get service {serviceType.Name} for plugin assembly {_pluginAssembly.GetName().Name}");
            throw;
        }
    }
}