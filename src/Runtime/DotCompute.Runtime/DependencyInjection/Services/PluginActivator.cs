// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace DotCompute.Runtime.DependencyInjection.Services;

/// <summary>
/// Default plugin activator implementation.
/// </summary>
internal sealed class PluginActivator(IServiceProvider serviceProvider) : IPluginActivator
{
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    public object CreateInstance(Type type) => ActivatorUtilities.CreateInstance(_serviceProvider, type);

    public T CreateInstance<T>() where T : class => ActivatorUtilities.CreateInstance<T>(_serviceProvider);
}