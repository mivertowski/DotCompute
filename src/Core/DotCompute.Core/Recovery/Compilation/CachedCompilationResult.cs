// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions;

namespace DotCompute.Core.Recovery.Compilation;

/// <summary>
/// Represents a cached compilation result that can be reused to avoid recompiling
/// kernels that have been successfully compiled with specific options.
/// </summary>
/// <remarks>
/// Cached compilation results are used to improve performance by avoiding
/// redundant compilation operations. The cache includes:
/// - A hash of the kernel for fast lookup and validation
/// - The original source code for verification
/// - The compilation options that resulted in success
/// - Expiration tracking to ensure cache freshness
///
/// The cache is particularly valuable for:
/// - Kernels that are compiled multiple times with the same parameters
/// - Fallback scenarios where a previously successful configuration can be reused
/// - Reducing compilation overhead in development and testing scenarios
///
/// Cached results include expiration times to ensure that outdated entries
/// are not used when the underlying compilation environment may have changed.
/// </remarks>
public class CachedCompilationResult
{
    /// <summary>
    /// Gets or sets the hash value of the kernel source code and compilation options.
    /// This hash is used for fast lookup and cache validation.
    /// </summary>
    /// <value>A hash string uniquely identifying the kernel and its compilation parameters.</value>
    public string KernelHash { get; set; } = string.Empty;


    /// <summary>
    /// Gets or sets the name of the kernel that was compiled.
    /// </summary>
    /// <value>The kernel identifier for reference and logging.</value>
    public string KernelName { get; set; } = string.Empty;


    /// <summary>
    /// Gets or sets the original source code of the kernel.
    /// This is stored for verification that the cached result matches the current request.
    /// </summary>
    /// <value>The complete kernel source code.</value>
    public string SourceCode { get; set; } = string.Empty;


    /// <summary>
    /// Gets or sets the compilation options that resulted in successful compilation.
    /// These options can be reused for identical compilation requests.
    /// </summary>
    /// <value>The compilation configuration that achieved success.</value>
    public CompilationOptions SuccessfulOptions { get; set; } = null!;


    /// <summary>
    /// Gets or sets the timestamp when this result was cached.
    /// </summary>
    /// <value>The UTC timestamp of when the compilation result was stored.</value>
    public DateTimeOffset Timestamp { get; set; }


    /// <summary>
    /// Gets or sets the timestamp when this cached result expires and should no longer be used.
    /// </summary>
    /// <value>The UTC timestamp after which this cache entry is considered invalid.</value>
    public DateTimeOffset ExpirationTime { get; set; }

    /// <summary>
    /// Gets a value indicating whether this cached result is still valid and can be used.
    /// </summary>
    /// <value>true if the current time is before the expiration time; otherwise, false.</value>
    public bool IsValid => DateTimeOffset.UtcNow < ExpirationTime;
}
