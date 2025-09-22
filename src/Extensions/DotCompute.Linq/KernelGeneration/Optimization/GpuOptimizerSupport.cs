// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Linq.KernelGeneration.Optimization
{
    /// <summary>
    /// Supporting classes for GPU optimization that were missing from the main file.
    /// </summary>
    public class OptimizationProfile
    {
        public bool EnableMemoryOptimizations { get; set; } = true;
        public bool EnableWarpOptimizations { get; set; } = true;
        public bool EnableSharedMemoryOptimizations { get; set; } = true;
        public bool EnableAtomicOptimizations { get; set; } = true;
        public bool EnableTextureMemory { get; set; } = false;
        public bool EnableConstantMemory { get; set; } = true;
        public bool SupportsTensorCores { get; set; } = false;
        public bool SupportsCooperativeGroups { get; set; } = false;
        public CacheOptimizationLevel CacheLevel { get; set; } = CacheOptimizationLevel.L1;
    }

    public enum CacheOptimizationLevel
    {
        None,
        L1,
        L2,
        L3
    }

    public class OptimizationCache : IDisposable
    {
        private readonly Dictionary<string, string> _cache = new();
        private bool _disposed;

        public bool TryGetOptimizedSource(string key, out string? source)
        {
            return _cache.TryGetValue(key, out source);
        }

        public void CacheOptimizedSource(string key, string source)
        {
            _cache[key] = source;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cache.Clear();
                _disposed = true;
            }
        }
    }
}