// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace DotCompute.Runtime.DependencyInjection.Scopes;

/// <summary>
/// Plugin-specific service scope wrapper.
/// </summary>
internal sealed class PluginServiceScope(IServiceProvider serviceProvider, IServiceScope innerScope) : IServiceScope
{
    private readonly IServiceScope _innerScope = innerScope ?? throw new ArgumentNullException(nameof(innerScope));
    private bool _disposed;

    public IServiceProvider ServiceProvider { get; } = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    public void Dispose()
    {
        if (!_disposed)
        {
            if (ServiceProvider is IDisposable disposableProvider)
            {
                disposableProvider.Dispose();
            }
            _innerScope.Dispose();
            _disposed = true;
        }
    }
}