#nullable enable

// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Primitives;

namespace DotCompute.Algorithms.Management.Types
{
    /// <summary>
    /// Null change token that never changes.
    /// </summary>
    internal sealed class NullChangeToken : IChangeToken
    {
        public static readonly NullChangeToken Singleton = new();
        /// <summary>
        /// Gets or sets a value indicating whether changed.
        /// </summary>
        /// <value>The has changed.</value>

        public bool HasChanged => false;
        /// <summary>
        /// Gets or sets the active change callbacks.
        /// </summary>
        /// <value>The active change callbacks.</value>
        public bool ActiveChangeCallbacks => false;
        /// <summary>
        /// Gets register change callback.
        /// </summary>
        /// <param name="callback">The callback.</param>
        /// <param name="state">The state.</param>
        /// <returns>The result of the operation.</returns>
        public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => NullDisposable.Instance;
    }
}