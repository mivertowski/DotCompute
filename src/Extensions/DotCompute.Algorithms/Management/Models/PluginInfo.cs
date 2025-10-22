
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Management.Models
{
    /// <summary>
    /// Basic plugin information.
    /// </summary>
    public sealed class PluginInfo
    {
        /// <summary>
        /// Gets or sets the plugin ID.
        /// </summary>
        public required string Id { get; set; }

        /// <summary>
        /// Gets or sets the plugin name.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Gets or sets the plugin version.
        /// </summary>
        public required Version Version { get; set; }

        /// <summary>
        /// Gets or sets the plugin description.
        /// </summary>
        public required string Description { get; set; }
    }
}