using System;
using System.Collections.Generic;

namespace DotCompute.Plugins.Configuration
{
    /// <summary>
    /// Options for the plugin system.
    /// </summary>
    public class PluginOptions
    {
        /// <summary>
        /// Gets or sets the directory to scan for plugins.
        /// </summary>
        public string? PluginsDirectory { get; set; }

        /// <summary>
        /// Gets or sets whether to enable hot reload.
        /// </summary>
        public bool EnableHotReload { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to load plugins in isolation.
        /// </summary>
        public bool IsolatePlugins { get; set; } = true;

        /// <summary>
        /// Gets or sets the list of shared assemblies.
        /// </summary>
        public List<string> SharedAssemblies { get; set; } = new()
        {
            "DotCompute.Core",
            "DotCompute.Plugins",
            "Microsoft.Extensions.DependencyInjection.Abstractions",
            "Microsoft.Extensions.Logging.Abstractions",
            "Microsoft.Extensions.Configuration.Abstractions"
        };

        /// <summary>
        /// Gets or sets configured plugins.
        /// </summary>
        public Dictionary<string, PluginConfig> Plugins { get; set; } = new();
    }

    /// <summary>
    /// Configuration for a specific plugin.
    /// </summary>
    public class PluginConfig
    {
        /// <summary>
        /// Gets or sets the plugin assembly path.
        /// </summary>
        public string AssemblyPath { get; set; } = "";

        /// <summary>
        /// Gets or sets the plugin type name.
        /// </summary>
        public string TypeName { get; set; } = "";

        /// <summary>
        /// Gets or sets whether the plugin is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets plugin-specific settings.
        /// </summary>
        public Dictionary<string, object> Settings { get; set; } = new();
    }
}