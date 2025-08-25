// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Plugins.Security;

namespace DotCompute.Plugins.Loaders.Internal;

/// <summary>
/// Cache entry for security scan results.
/// </summary>
internal class SecurityScanCache
{
    public required SecurityScanResult ScanResult { get; set; }
    public DateTimeOffset CachedAt { get; set; }
    public bool IsExpired => DateTimeOffset.UtcNow - CachedAt > TimeSpan.FromHours(1);
}