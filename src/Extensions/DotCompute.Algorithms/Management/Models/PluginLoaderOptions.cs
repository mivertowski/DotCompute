
// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Algorithms.Management.Models
{
    /// <summary>
    /// Configuration options for the PluginLoader.
    /// </summary>
    public sealed class PluginLoaderOptions
    {
        /// <summary>
        /// Gets or sets whether plugin isolation is enabled.
        /// </summary>
        public bool EnableIsolation { get; set; } = true;

        /// <summary>
        /// Gets or sets whether signed assemblies are required.
        /// </summary>
        public bool RequireSignedAssemblies { get; set; } = true;

        /// <summary>
        /// Gets or sets whether strong names are required.
        /// </summary>
        public bool RequireStrongName { get; set; } = true;

        /// <summary>
        /// Gets or sets whether malware scanning is enabled.
        /// </summary>
        public bool EnableMalwareScanning { get; set; }


        /// <summary>
        /// Gets or sets the maximum assembly size in bytes.
        /// </summary>
        public long MaxAssemblySize { get; set; } = 100 * 1024 * 1024; // 100 MB

        /// <summary>
        /// Gets or sets the trusted public key XML for signature validation.
        /// </summary>
        public string? TrustedPublicKeyXml { get; set; }

        /// <summary>
        /// Gets the list of allowed directories for loading plugins.
        /// </summary>
        public IList<string> AllowedDirectories { get; } = [];

        /// <summary>
        /// Gets the list of trusted assembly hashes.
        /// </summary>
        public HashSet<string> TrustedAssemblyHashes { get; } = [];

        /// <summary>
        /// Gets or sets the sandbox configuration for plugin execution.
        /// </summary>
        public PluginSandboxOptions SandboxOptions { get; set; } = new();
    }
}