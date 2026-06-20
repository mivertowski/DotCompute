// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.Metal.Native;

namespace DotCompute.Hardware.Metal.Tests;

/// <summary>
/// Shared, DllNotFound-safe gate for Metal availability used across the Metal hardware-test suite.
/// </summary>
/// <remarks>
/// <para>
/// The raw native probe <see cref="MetalNative.IsMetalSupported"/> is a <c>[LibraryImport]</c>
/// P/Invoke into <c>libDotComputeMetal</c>. On any platform where that native library cannot be
/// loaded (e.g. Linux/Windows CI runners — Metal only exists on macOS) calling it throws
/// <see cref="DllNotFoundException"/> (or other load-time exceptions) <b>before</b> a test's
/// <c>Skip.IfNot(...)</c> can ever observe the result.
/// </para>
/// <para>
/// This helper wraps the native probe in a try/catch and caches the result, so the very first
/// access can never escape an exception: any load/marshal/platform failure is treated as
/// "Metal not available" and the gated test skips cleanly instead of failing. The real Metal
/// coverage runs on the macOS nightly CI job, where <see cref="IsMetalAvailable"/> returns
/// <see langword="true"/> and the tests execute for real.
/// </para>
/// </remarks>
public static class MetalTestEnvironment
{
    private static readonly Lazy<bool> _isMetalAvailable = new(ProbeMetalSafe);

    /// <summary>
    /// Gets a value indicating whether Metal is available on the current machine.
    /// Cached after the first probe. Returns <see langword="false"/> on any platform where the
    /// native Metal library cannot be loaded or the device probe throws, rather than propagating
    /// the exception.
    /// </summary>
    public static bool IsMetalAvailable => _isMetalAvailable.Value;

    private static bool ProbeMetalSafe()
    {
        try
        {
            return MetalNative.IsMetalSupported();
        }
        catch
        {
            // DllNotFoundException on non-macOS platforms, or any other native load/probe failure:
            // treat as "Metal unavailable" so dependent tests skip instead of failing.
            return false;
        }
    }
}
