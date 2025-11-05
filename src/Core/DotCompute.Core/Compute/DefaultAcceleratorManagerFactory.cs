// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Core.Compute
{
    /// <summary>
    /// Factory for creating DefaultAcceleratorManager instances without dependency injection.
    /// </summary>
    /// <remarks>
    /// This factory provides a simple way to create accelerator managers for standalone applications
    /// that don't use Microsoft.Extensions.DependencyInjection. For applications using DI,
    /// prefer registering services via AddDotComputeRuntime() instead.
    /// </remarks>
    public static class DefaultAcceleratorManagerFactory
    {
        /// <summary>
        /// Creates and initializes a new accelerator manager asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for the initialization operation.</param>
        /// <returns>
        /// A fully initialized accelerator manager ready for use.
        /// </returns>
        /// <remarks>
        /// This method creates a manager with a null logger (no logging output).
        /// For production applications, consider using dependency injection
        /// to provide proper logging infrastructure.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Create manager without DI:
        /// var manager = await DefaultAcceleratorManagerFactory.CreateAsync();
        ///
        /// // Use manager:
        /// var accelerators = await manager.GetAcceleratorsAsync();
        /// foreach (var acc in accelerators)
        /// {
        ///     Console.WriteLine($"Found: {acc.Info.Name}");
        /// }
        ///
        /// // Cleanup:
        /// await manager.DisposeAsync();
        /// </code>
        /// </example>
        public static async Task<IAcceleratorManager> CreateAsync(
            CancellationToken cancellationToken = default)
        {
            var logger = NullLogger<DefaultAcceleratorManager>.Instance;
            var manager = new DefaultAcceleratorManager(logger);
            await manager.InitializeAsync(cancellationToken);
            return manager;
        }

        /// <summary>
        /// Creates and initializes a new accelerator manager with custom logger asynchronously.
        /// </summary>
        /// <param name="logger">Logger instance for diagnostic output.</param>
        /// <param name="cancellationToken">Cancellation token for the initialization operation.</param>
        /// <returns>
        /// A fully initialized accelerator manager ready for use.
        /// </returns>
        /// <remarks>
        /// This overload allows providing a custom logger while still avoiding full DI setup.
        /// Useful for applications that need logging but don't use DI containers.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Create logger:
        /// using var loggerFactory = LoggerFactory.Create(builder =>
        /// {
        ///     builder.AddConsole().SetMinimumLevel(LogLevel.Information);
        /// });
        /// var logger = loggerFactory.CreateLogger&lt;DefaultAcceleratorManager&gt;();
        ///
        /// // Create manager with logger:
        /// var manager = await DefaultAcceleratorManagerFactory.CreateAsync(logger);
        ///
        /// // Use manager (logs will be output):
        /// await manager.InitializeAsync();
        /// </code>
        /// </example>
        public static async Task<IAcceleratorManager> CreateAsync(
            ILogger<DefaultAcceleratorManager> logger,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(logger);

            var manager = new DefaultAcceleratorManager(logger);
            await manager.InitializeAsync(cancellationToken);
            return manager;
        }

        /// <summary>
        /// Creates a new accelerator manager synchronously (not initialized).
        /// </summary>
        /// <returns>
        /// An uninitialized accelerator manager. Call InitializeAsync() before use.
        /// </returns>
        /// <remarks>
        /// This method creates the manager but does not initialize it.
        /// You MUST call InitializeAsync() before using the manager.
        /// Prefer CreateAsync() for simpler usage.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Create uninitialized manager:
        /// var manager = DefaultAcceleratorManagerFactory.Create();
        ///
        /// // Initialize before use:
        /// await manager.InitializeAsync();
        ///
        /// // Now ready to use:
        /// var accelerators = await manager.GetAcceleratorsAsync();
        /// </code>
        /// </example>
        public static IAcceleratorManager Create()
        {
            var logger = NullLogger<DefaultAcceleratorManager>.Instance;
            return new DefaultAcceleratorManager(logger);
        }

        /// <summary>
        /// Creates a new accelerator manager with custom logger synchronously (not initialized).
        /// </summary>
        /// <param name="logger">Logger instance for diagnostic output.</param>
        /// <returns>
        /// An uninitialized accelerator manager. Call InitializeAsync() before use.
        /// </returns>
        /// <remarks>
        /// This method creates the manager but does not initialize it.
        /// You MUST call InitializeAsync() before using the manager.
        /// Prefer CreateAsync() for simpler usage.
        /// </remarks>
        public static IAcceleratorManager Create(ILogger<DefaultAcceleratorManager> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);

            return new DefaultAcceleratorManager(logger);
        }
    }
}
