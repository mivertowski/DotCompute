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
        public string? this[string key]
        {
            get => null;
            set { }
        }

        public string Key { get; } = key;
        public string Path { get; } = key;
        public string? Value { get; set; }

        public IEnumerable<IConfigurationSection> GetChildren() => [];
        public IChangeToken GetReloadToken() => Types.NullChangeToken.Singleton;
        public IConfigurationSection GetSection(string key) => new MinimalConfigurationSection($"{Path}:{key}");
    }
}