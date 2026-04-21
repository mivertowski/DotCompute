// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;
using Xunit;

namespace DotCompute.Tests.Common;

/// <summary>
/// Gates tests on detected CUDA compute capability. Use <see cref="RequireMinimumCapability(int, int)"/>
/// (or one of the convenience wrappers) at the top of a <c>[SkippableFact]</c> to ring-fence
/// tests to devices of a given compute capability or newer.
/// </summary>
/// <remarks>
/// <para>
/// The <c>DotCompute.Tests.Common</c> assembly deliberately does not reference
/// <c>DotCompute.Backends.CUDA</c> (see its <c>.csproj</c> comment). To avoid introducing a
/// project-reference cycle, this helper declares its own minimal P/Invoke surface for
/// <c>cudaGetDevice</c> and <c>cudaDeviceGetAttribute</c>. Attributes 75 and 76 are
/// <c>cudaDevAttrComputeCapabilityMajor</c> and <c>...Minor</c> respectively in the CUDA
/// Runtime API; these values are stable across CUDA 11/12/13.
/// </para>
/// <para>
/// All calls are best-effort: if the CUDA runtime is not installed or loadable, the helper
/// reports "not available" rather than throwing, and the calling test is skipped via
/// <see cref="Skip.IfNot(bool, string)"/>.
/// </para>
/// </remarks>
public static class CudaTestGate
{
    // Linux/macOS library name (resolves libcudart.so / .so.N through runtime search).
    // On Windows, cudart64_* is resolved via DllImportSearchPath.SafeDirectories.
    private const string CudaRuntimeLibrary = "cudart";

    // Compute capability attribute IDs from CUDA's cudaDeviceAttr enum. These map to
    // cudaDevAttrComputeCapabilityMajor (75) and cudaDevAttrComputeCapabilityMinor (76).
    private const int AttrComputeCapabilityMajor = 75;
    private const int AttrComputeCapabilityMinor = 76;

    [DllImport(CudaRuntimeLibrary)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern int cudaGetDevice(out int device);

    [DllImport(CudaRuntimeLibrary)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static extern int cudaDeviceGetAttribute(ref int value, int attr, int device);

    /// <summary>
    /// Attempts to detect the current CUDA device's compute capability (major, minor).
    /// </summary>
    /// <param name="major">On success, the major compute capability (e.g. 9 for Hopper). 0 on failure.</param>
    /// <param name="minor">On success, the minor compute capability (e.g. 0). 0 on failure.</param>
    /// <returns><c>true</c> if a CUDA device was found and its compute capability was read; <c>false</c> otherwise.</returns>
    /// <remarks>
    /// This call is best-effort and silently swallows any P/Invoke or runtime errors, returning <c>(false, 0, 0)</c>.
    /// Intended for use from test gates where "can't detect" and "no CUDA" are equivalent outcomes.
    /// </remarks>
    public static bool TryGetCurrentDeviceCapability(out int major, out int minor)
    {
        major = 0;
        minor = 0;

        try
        {
            // cudaSuccess is 0. Any non-zero return indicates a driver/runtime issue — treat as unavailable.
            if (cudaGetDevice(out var deviceId) != 0)
            {
                return false;
            }

            int majorValue = 0;
            if (cudaDeviceGetAttribute(ref majorValue, AttrComputeCapabilityMajor, deviceId) != 0)
            {
                return false;
            }

            int minorValue = 0;
            if (cudaDeviceGetAttribute(ref minorValue, AttrComputeCapabilityMinor, deviceId) != 0)
            {
                return false;
            }

            major = majorValue;
            minor = minorValue;
            return true;
        }
        catch (DllNotFoundException)
        {
            // CUDA runtime not installed on this machine.
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            // Runtime is too old / missing symbol — treat as unavailable.
            return false;
        }
        catch (Exception)
        {
            // Defensive: never throw from a test gate.
            return false;
        }
    }

    /// <summary>
    /// Skips the calling <c>[SkippableFact]</c> test unless a CUDA device with at least the
    /// given compute capability is present.
    /// </summary>
    /// <param name="requiredMajor">The minimum required major compute capability.</param>
    /// <param name="requiredMinor">The minimum required minor compute capability.</param>
    /// <remarks>
    /// Capabilities are compared lexicographically: <c>(major, minor) &gt;= (requiredMajor, requiredMinor)</c>.
    /// </remarks>
    public static void RequireMinimumCapability(int requiredMajor, int requiredMinor)
    {
        var detected = TryGetCurrentDeviceCapability(out var major, out var minor);
        var satisfied = detected && (major > requiredMajor || (major == requiredMajor && minor >= requiredMinor));

        var reason = detected
            ? $"Requires CUDA compute capability {requiredMajor}.{requiredMinor}+; detected {major}.{minor}."
            : $"Requires CUDA compute capability {requiredMajor}.{requiredMinor}+; no CUDA device detected.";

        Skip.IfNot(satisfied, reason);
    }

    /// <summary>
    /// Convenience wrapper for <see cref="RequireMinimumCapability(int, int)"/> that gates the
    /// calling test on a Hopper-class (CC 9.0) or newer device.
    /// </summary>
    public static void RequireHopper() => RequireMinimumCapability(9, 0);

    /// <summary>
    /// Convenience wrapper for <see cref="RequireMinimumCapability(int, int)"/> that gates the
    /// calling test on an Ada-class (CC 8.9) or newer device.
    /// </summary>
    public static void RequireAdaOrNewer() => RequireMinimumCapability(8, 9);
}
