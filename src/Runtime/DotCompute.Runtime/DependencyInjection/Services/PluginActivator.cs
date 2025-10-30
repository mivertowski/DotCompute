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
    /// <summary>
    /// Creates a new instance.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The created instance.</returns>

    public object CreateInstance(Type type) => ActivatorUtilities.CreateInstance(_serviceProvider, type);
    /// <summary>
    /// Creates a new instance.
    /// </summary>
    /// <typeparam name="T">The T type parameter.</typeparam>
    /// <returns>The created instance.</returns>

    public T CreateInstance<T>() where T : class => ActivatorUtilities.CreateInstance<T>(_serviceProvider);
}
