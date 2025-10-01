// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using DotCompute.Algorithms.Management.Types;

namespace DotCompute.Algorithms.Management.Services
{
    /// <summary>
    /// Minimal configuration section for plugin initialization.
    /// </summary>
    internal sealed class MinimalConfigurationSection : IConfigurationSection
    {
        public MinimalConfigurationSection(string key)
        {
            Key = key;
            Path = key;
        }

        public string? this[string key]
        {
            get => null;
            set { }
        }

        public string Key { get; }
        public string Path { get; }
        public string? Value { get; set; }

        public IEnumerable<IConfigurationSection> GetChildren() => [];
        public IChangeToken GetReloadToken() => Types.NullChangeToken.Singleton;
        public IConfigurationSection GetSection(string key) => new MinimalConfigurationSection($"{Path}:{key}");
    }
}