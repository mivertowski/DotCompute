// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Plugins.Security;

namespace DotCompute.Plugins.Loaders.Internal;

/// <summary>
/// Cache entry for security scan results.
/// </summary>
internal class SecurityScanCache
{
    /// <summary>
    /// Gets or sets the scan result.
    /// </summary>
    /// <value>The scan result.</value>
    public required SecurityScanResult ScanResult { get; set; }
    /// <summary>
    /// Gets or sets the cached at.
    /// </summary>
    /// <value>The cached at.</value>
    public DateTimeOffset CachedAt { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether expired.
    /// </summary>
    /// <value>The is expired.</value>
    public bool IsExpired => DateTimeOffset.UtcNow - CachedAt > TimeSpan.FromHours(1);
}