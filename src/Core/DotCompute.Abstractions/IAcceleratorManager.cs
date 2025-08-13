// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions;

/// <summary>
/// Manages accelerator devices and their lifecycle.
/// </summary>
public interface IAcceleratorManager : IAsyncDisposable
{
    /// <summary>
    /// Gets the default accelerator instance.
    /// </summary>
    public IAccelerator Default { get; }

    /// <summary>
    /// Gets all available accelerators.
    /// </summary>
    public IReadOnlyList<IAccelerator> AvailableAccelerators { get; }

    /// <summary>
    /// Gets the number of available accelerators.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Discovers and initializes all available accelerators.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    public ValueTask InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an accelerator by index.
    /// </summary>
    /// <param name="index">The index of the accelerator.</param>
    /// <returns>The accelerator at the specified index.</returns>
    public IAccelerator GetAccelerator(int index);

    /// <summary>
    /// Gets an accelerator by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the accelerator.</param>
    /// <returns>The accelerator with the specified ID, or null if not found.</returns>
    public IAccelerator? GetAcceleratorById(string id);

    /// <summary>
    /// Gets accelerators of a specific type.
    /// </summary>
    /// <param name="type">The type of accelerators to get.</param>
    /// <returns>A list of accelerators of the specified type.</returns>
    public IEnumerable<IAccelerator> GetAcceleratorsByType(AcceleratorType type);

    /// <summary>
    /// Selects the best accelerator based on the given criteria.
    /// </summary>
    /// <param name="criteria">The selection criteria.</param>
    /// <returns>The best matching accelerator, or null if none match.</returns>
    public IAccelerator? SelectBest(AcceleratorSelectionCriteria criteria);

    /// <summary>
    /// Creates a new accelerator context for the specified accelerator.
    /// </summary>
    /// <param name="accelerator">The accelerator to create a context for.</param>
    /// <returns>A new accelerator context.</returns>
    public AcceleratorContext CreateContext(IAccelerator accelerator);

    /// <summary>
    /// Registers a custom accelerator provider.
    /// </summary>
    /// <param name="provider">The accelerator provider to register.</param>
    public void RegisterProvider(IAcceleratorProvider provider);

    /// <summary>
    /// Refreshes the list of available accelerators.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation.</returns>
    public ValueTask RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all available accelerators asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation that returns all available accelerators.</returns>
    public Task<IEnumerable<IAccelerator>> GetAcceleratorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets accelerators of a specific type asynchronously.
    /// </summary>
    /// <param name="type">The type of accelerators to get.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation that returns accelerators of the specified type.</returns>
    public Task<IEnumerable<IAccelerator>> GetAcceleratorsAsync(AcceleratorType type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the best accelerator for the specified type asynchronously.
    /// </summary>
    /// <param name="type">The preferred accelerator type, or null for any type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the async operation that returns the best matching accelerator, or null if none match.</returns>
    public Task<IAccelerator?> GetBestAcceleratorAsync(AcceleratorType? type = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Criteria for selecting an accelerator.
/// </summary>
public sealed class AcceleratorSelectionCriteria
{
    /// <summary>
    /// Gets or sets the minimum required memory in bytes.
    /// </summary>
    public long? MinimumMemory { get; set; }

    /// <summary>
    /// Gets or sets the preferred accelerator type.
    /// </summary>
    public AcceleratorType? PreferredType { get; set; }

    /// <summary>
    /// Gets or sets the required features.
    /// </summary>
    public AcceleratorFeature? RequiredFeatures { get; set; }

    /// <summary>
    /// Gets or sets the minimum compute capability.
    /// </summary>
    public Version? MinimumComputeCapability { get; set; }

    /// <summary>
    /// Gets or sets whether to prefer dedicated over integrated devices.
    /// </summary>
    public bool PreferDedicated { get; set; } = true;

    /// <summary>
    /// Gets or sets a custom scoring function for ranking accelerators.
    /// </summary>
    public Func<IAccelerator, double>? CustomScorer { get; set; }
}

/// <summary>
/// Provides accelerator instances.
/// </summary>
public interface IAcceleratorProvider
{
    /// <summary>
    /// Gets the name of this provider.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the types of accelerators this provider can create.
    /// </summary>
    public AcceleratorType[] SupportedTypes { get; }

    /// <summary>
    /// Discovers available accelerators.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of discovered accelerators.</returns>
    public ValueTask<IEnumerable<IAccelerator>> DiscoverAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an accelerator instance.
    /// </summary>
    /// <param name="info">The accelerator information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created accelerator instance.</returns>
    public ValueTask<IAccelerator> CreateAsync(AcceleratorInfo info, CancellationToken cancellationToken = default);
}
