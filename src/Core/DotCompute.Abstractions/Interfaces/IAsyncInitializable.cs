// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Interfaces;

/// <summary>
/// Interface for components that support asynchronous initialization.
/// </summary>
/// <remarks>
/// Implement this interface when a component requires async setup work
/// before it can be used, such as loading resources, establishing connections,
/// or performing device initialization.
/// </remarks>
public interface IAsyncInitializable
{
    /// <summary>
    /// Initializes the component asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the initialization.</param>
    /// <returns>A task representing the asynchronous initialization operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the component is already initialized.</exception>
    /// <exception cref="OperationCanceledException">Thrown if initialization is canceled.</exception>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a value indicating whether the component has been initialized.
    /// </summary>
    bool IsInitialized { get; }
}
