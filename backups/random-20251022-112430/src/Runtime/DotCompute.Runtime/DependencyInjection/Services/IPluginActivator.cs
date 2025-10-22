// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Runtime.DependencyInjection.Services;

/// <summary>
/// Plugin activator interface.
/// </summary>
public interface IPluginActivator
{
    /// <summary>
    /// Creates an instance of the specified type with dependency injection.
    /// </summary>
    /// <param name="type">The type to create.</param>
    /// <returns>The created instance.</returns>
    public object CreateInstance(Type type);

    /// <summary>
    /// Creates an instance of the specified type.
    /// </summary>
    /// <typeparam name="T">The type to create.</typeparam>
    /// <returns>The created instance.</returns>
    public T CreateInstance<T>() where T : class;
}