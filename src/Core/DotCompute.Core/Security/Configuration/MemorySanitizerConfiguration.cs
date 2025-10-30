// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Security.Configuration;

/// <summary>
/// Configuration for memory sanitizer behavior.
/// Provides comprehensive settings for memory protection, sanitization policies,
/// and security thresholds.
/// </summary>
public sealed class MemorySanitizerConfiguration
{
    /// <summary>
    /// Gets the default configuration with security-focused settings.
    /// </summary>
    public static MemorySanitizerConfiguration Default => new()
    {
        EnableGuardBytes = true,
        GuardByteSize = 16,
        EnableCanaryValues = true,
        EnableSecureWiping = true,
        InitializeWithRandomData = false,
        MaxAllocationSize = 1024 * 1024 * 1024, // 1GB
        LeakDetectionThreshold = TimeSpan.FromMinutes(30)
    };

    /// <summary>
    /// Gets or sets whether guard bytes are enabled for buffer overflow detection.
    /// </summary>
    public bool EnableGuardBytes { get; init; } = true;

    /// <summary>
    /// Gets or sets the size of guard byte regions around allocations.
    /// </summary>
    public nuint GuardByteSize { get; init; } = 16;

    /// <summary>
    /// Gets or sets whether canary values are used for corruption detection.
    /// </summary>
    public bool EnableCanaryValues { get; init; } = true;

    /// <summary>
    /// Gets or sets whether secure wiping is performed on deallocation.
    /// </summary>
    public bool EnableSecureWiping { get; init; } = true;

    /// <summary>
    /// Gets or sets whether memory is initialized with random data for security.
    /// </summary>
    public bool InitializeWithRandomData { get; init; }

    /// <summary>
    /// Gets or sets the maximum size of a single allocation.
    /// </summary>
    public nuint MaxAllocationSize { get; init; } = 1024 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the threshold for memory leak detection.
    /// </summary>
    public TimeSpan LeakDetectionThreshold { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Returns a string representation of the configuration.
    /// </summary>
    public override string ToString()
        => $"Guards={EnableGuardBytes}, Canaries={EnableCanaryValues}, SecureWipe={EnableSecureWiping}";
}
