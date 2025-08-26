// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;

using System;
namespace DotCompute.Core.Compute
{

    /// <summary>
    /// Default implementation of IAcceleratorManager.
    /// </summary>
    public class DefaultAcceleratorManager(ILogger<DefaultAcceleratorManager> logger) : IAcceleratorManager
    {
        private readonly ILogger<DefaultAcceleratorManager> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly List<IAcceleratorProvider> _providers = [];
        private readonly List<IAccelerator> _accelerators = [];
        private IAccelerator? _default;
        private bool _initialized;
        private bool _disposed;

        /// <summary>
        /// Gets the default accelerator instance.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// AcceleratorManager must be initialized before accessing Default
        /// or
        /// No default accelerator available
        /// </exception>
        public IAccelerator Default
        {
            get
            {
                if (!_initialized)
                {
                    throw new InvalidOperationException("AcceleratorManager must be initialized before accessing Default");
                }

                return _default ?? throw new InvalidOperationException("No default accelerator available");
            }
        }

        /// <summary>
        /// Gets all available accelerators.
        /// </summary>
        public IReadOnlyList<IAccelerator> AvailableAccelerators => _accelerators.AsReadOnly();

        /// <summary>
        /// Gets the number of available accelerators.
        /// </summary>
        public int Count => _accelerators.Count;

        private static readonly char[] _separator = ['-', '_'];

        /// <summary>
        /// Discovers and initializes all available accelerators.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// A task representing the async operation.
        /// </returns>
        public async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_initialized)
            {
                return;
            }

            _logger.LogInformation("Initializing accelerator manager");

            // Discover accelerators from all providers
            foreach (var provider in _providers)
            {
                try
                {
                    var accelerators = await provider.DiscoverAsync(cancellationToken);
                    _accelerators.AddRange(accelerators);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to discover accelerators from provider {Provider}", provider.Name);
                }
            }

            // Select default accelerator (prefer GPU, fallback to CPU)
            _default = _accelerators.FirstOrDefault(a => a.Info.DeviceType == "GPU") ??
                       _accelerators.FirstOrDefault(a => a.Info.DeviceType == "CPU") ??
                       _accelerators.FirstOrDefault();

            _initialized = true;
            _logger.LogInformation("Initialized with {Count} accelerators", _accelerators.Count);
        }

        /// <summary>
        /// Gets an accelerator by index.
        /// </summary>
        /// <param name="index">The index of the accelerator.</param>
        /// <returns>
        /// The accelerator at the specified index.
        /// </returns>
        /// <exception cref="InvalidOperationException">AcceleratorManager must be initialized</exception>
        /// <exception cref="ArgumentOutOfRangeException">index</exception>
        public IAccelerator GetAccelerator(int index)
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("AcceleratorManager must be initialized");
            }

            if (index < 0 || index >= _accelerators.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _accelerators[index];
        }

        /// <summary>
        /// Gets an accelerator by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier of the accelerator.</param>
        /// <returns>
        /// The accelerator with the specified ID, or null if not found.
        /// </returns>
        /// <exception cref="InvalidOperationException">AcceleratorManager must be initialized</exception>
        public IAccelerator? GetAcceleratorById(string id)
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("AcceleratorManager must be initialized");
            }

            return _accelerators.FirstOrDefault(a => a.Info.Id == id);
        }

        /// <summary>
        /// Gets accelerators of a specific type.
        /// </summary>
        /// <param name="type">The type of accelerators to get.</param>
        /// <returns>
        /// A list of accelerators of the specified type.
        /// </returns>
        /// <exception cref="InvalidOperationException">AcceleratorManager must be initialized</exception>
        public IEnumerable<IAccelerator> GetAcceleratorsByType(AcceleratorType type)
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("AcceleratorManager must be initialized");
            }

            var typeString = type.ToString();
            return _accelerators.Where(a => a.Info.DeviceType.Equals(typeString, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Selects the best accelerator based on the given criteria.
        /// </summary>
        /// <param name="criteria">The selection criteria.</param>
        /// <returns>
        /// The best matching accelerator, or null if none match.
        /// </returns>
        /// <exception cref="InvalidOperationException">AcceleratorManager must be initialized</exception>
        public IAccelerator? SelectBest(AcceleratorSelectionCriteria criteria)
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("AcceleratorManager must be initialized");
            }

            var candidates = _accelerators.AsEnumerable();

            // Filter by type
            if (criteria.PreferredType.HasValue)
            {
                var typeString = criteria.PreferredType.Value.ToString();
                candidates = candidates.Where(a => a.Info.DeviceType.Equals(typeString, StringComparison.OrdinalIgnoreCase));
            }

            // Filter by memory
            if (criteria.MinimumMemory.HasValue)
            {
                candidates = candidates.Where(a => a.Info.TotalMemory >= criteria.MinimumMemory.Value);
            }

            // Filter by compute capability
            if (criteria.MinimumComputeCapability != null)
            {
                candidates = candidates.Where(a => a.Info.ComputeCapability != null &&
                                                 a.Info.ComputeCapability >= criteria.MinimumComputeCapability);
            }

            // Apply custom scorer or default scoring
            if (criteria.CustomScorer != null)
            {
                return candidates.OrderByDescending(criteria.CustomScorer).FirstOrDefault();
            }

            // Default scoring: prefer dedicated, then by memory
            return candidates
                .OrderByDescending(a => a.Info.TotalMemory)
                .FirstOrDefault();
        }

        /// <summary>
        /// Creates a new accelerator context for the specified accelerator.
        /// </summary>
        /// <param name="accelerator">The accelerator to create a context for.</param>
        /// <returns>
        /// A new accelerator context.
        /// </returns>
        /// <exception cref="InvalidOperationException">AcceleratorManager must be initialized</exception>
        /// <exception cref="ArgumentException">Accelerator is not managed by this manager - accelerator</exception>
        public AcceleratorContext CreateContext(IAccelerator accelerator)
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("AcceleratorManager must be initialized");
            }

            if (!_accelerators.Contains(accelerator))
            {
                throw new ArgumentException("Accelerator is not managed by this manager", nameof(accelerator));
            }

            // For CPU accelerators, create a context with the thread ID as the handle
            // Try to extract a numeric device ID from the accelerator ID
            var idParts = accelerator.Info.Id.Split(_separator);
            var lastPart = idParts.LastOrDefault() ?? "0";
            // Try to parse the last part as a number, otherwise use 0
            if (!int.TryParse(lastPart, out var deviceId))
            {
                deviceId = 0;
            }

            // Use the current thread ID as the device context handle for CPU
            // This provides a unique, valid handle for each context
            var contextHandle = new IntPtr(Environment.CurrentManagedThreadId);

            return new AcceleratorContext(contextHandle, deviceId);
        }

        /// <summary>
        /// Registers a custom accelerator provider.
        /// </summary>
        /// <param name="provider">The accelerator provider to register.</param>
        /// <exception cref="InvalidOperationException">Cannot register providers after initialization</exception>
        /// <exception cref="ArgumentNullException">provider</exception>
        public void RegisterProvider(IAcceleratorProvider provider)
        {
            if (_initialized)
            {
                throw new InvalidOperationException("Cannot register providers after initialization");
            }

            _providers.Add(provider ?? throw new ArgumentNullException(nameof(provider)));
            _logger.LogInformation("Registered accelerator provider: {Provider}", provider.Name);
        }

        /// <summary>
        /// Refreshes the list of available accelerators.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// A task representing the async operation.
        /// </returns>
        public async ValueTask RefreshAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Refreshing accelerator list");

            _accelerators.Clear();
            _default = null;
            _initialized = false;

            await InitializeAsync(cancellationToken);
        }

        /// <summary>
        /// Gets all available accelerators asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// A task representing the async operation that returns all available accelerators.
        /// </returns>
        /// <exception cref="InvalidOperationException">AcceleratorManager must be initialized</exception>
        public Task<IEnumerable<IAccelerator>> GetAcceleratorsAsync(CancellationToken cancellationToken = default)
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("AcceleratorManager must be initialized");
            }

            return Task.FromResult<IEnumerable<IAccelerator>>(_accelerators.AsReadOnly());
        }


        /// <summary>
        /// Gets accelerators of a specific type asynchronously.
        /// </summary>
        /// <param name="type">The type of accelerators to get.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// A task representing the async operation that returns accelerators of the specified type.
        /// </returns>
        /// <exception cref="InvalidOperationException">AcceleratorManager must be initialized</exception>
        public Task<IEnumerable<IAccelerator>> GetAcceleratorsAsync(AcceleratorType type, CancellationToken cancellationToken = default)
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("AcceleratorManager must be initialized");
            }

            var result = GetAcceleratorsByType(type);
            return Task.FromResult(result);
        }

        /// <summary>
        /// Gets the best accelerator for the specified type asynchronously.
        /// </summary>
        /// <param name="type">The preferred accelerator type, or null for any type.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// A task representing the async operation that returns the best matching accelerator, or null if none match.
        /// </returns>
        /// <exception cref="InvalidOperationException">AcceleratorManager must be initialized</exception>
        public Task<IAccelerator?> GetBestAcceleratorAsync(AcceleratorType? type = null, CancellationToken cancellationToken = default)
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("AcceleratorManager must be initialized");
            }

            IAccelerator? result;
            if (type.HasValue)
            {
                var criteria = new AcceleratorSelectionCriteria { PreferredType = type.Value };
                result = SelectBest(criteria);
            }
            else
            {
                result = Default;
            }

            return Task.FromResult(result);
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _logger.LogInformation("Disposing accelerator manager");

            // Dispose all accelerators
            foreach (var accelerator in _accelerators)
            {
                try
                {
                    await accelerator.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to dispose accelerator {Id}", accelerator.Info.Id);
                }
            }

            _accelerators.Clear();
            _providers.Clear();
            _default = null;
            _disposed = true;
        }
    }
}
