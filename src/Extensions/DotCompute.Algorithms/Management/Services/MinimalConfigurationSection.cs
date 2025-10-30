
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace DotCompute.Algorithms.Management.Services
{
    /// <summary>
    /// Minimal configuration section for plugin initialization.
    /// </summary>
    internal sealed class MinimalConfigurationSection(string key) : IConfigurationSection
    {
        /// <summary>
        /// Gets or sets the this[].
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The value at the specified index.</returns>
        public string? this[string key]
        {
            get => null;
            set { }
        }
        /// <summary>
        /// Gets or sets the key.
        /// </summary>
        /// <value>The key.</value>

        public string Key { get; } = key;
        /// <summary>
        /// Gets or sets the path.
        /// </summary>
        /// <value>The path.</value>
        public string Path { get; } = key;
        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        /// <value>The value.</value>
        public string? Value { get; set; }
        /// <summary>
        /// Gets the children.
        /// </summary>
        /// <returns>The children.</returns>

        public IEnumerable<IConfigurationSection> GetChildren() => [];
        /// <summary>
        /// Gets the reload token.
        /// </summary>
        /// <returns>The reload token.</returns>
        public IChangeToken GetReloadToken() => Types.NullChangeToken.Singleton;
        /// <summary>
        /// Gets the section.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>The section.</returns>
        public IConfigurationSection GetSection(string key) => new MinimalConfigurationSection($"{Path}:{key}");
    }
}
