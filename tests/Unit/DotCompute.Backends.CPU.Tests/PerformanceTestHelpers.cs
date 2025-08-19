// Copyright(c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.


using System.Runtime.InteropServices;

namespace DotCompute.Backends.CPU;


/// <summary>
/// Helper methods for performance tests that account for different environments.
/// </summary>
internal static class PerformanceTestHelpers
{
    /// <summary>
    /// Gets whether we're running in a virtualized environment like WSL.
    /// </summary>
    public static bool IsVirtualizedEnvironment()
    {
        // Check for WSL
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            try
            {
                var osRelease = System.IO.File.ReadAllText("/proc/version");
                if (osRelease.Contains("microsoft", StringComparison.OrdinalIgnoreCase) ||
                    osRelease.Contains("WSL", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch { }
        }

        // Check for common virtualization indicators
        try
        {
            var cpuInfo = System.IO.File.ReadAllText("/proc/cpuinfo");
            if (cpuInfo.Contains("hypervisor", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        catch { }

        return false;
    }

    /// <summary>
    /// Gets the minimum expected speedup for a given test, accounting for environment.
    /// </summary>
    public static double GetMinimumExpectedSpeedup(double baselineSpeedup, string testName = "")
    {
        // In virtualized environments, performance can be significantly lower
        if (IsVirtualizedEnvironment())
        {
            // Reduce expectations by 40% in virtual environments
            return baselineSpeedup * 0.6;
        }

        // On CI/CD systems, performance can also be lower
        if (Environment.GetEnvironmentVariable("CI") != null ||
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != null)
        {
            // Reduce expectations by 30% on CI
            return baselineSpeedup * 0.7;
        }

        // Otherwise use baseline
        return baselineSpeedup;
    }

    /// <summary>
    /// Asserts that the speedup meets minimum requirements for the environment.
    /// </summary>
    public static void AssertSpeedupWithinExpectedRange(double actualSpeedup, double baselineExpectedSpeedup, string testName)
    {
        var minimumSpeedup = GetMinimumExpectedSpeedup(baselineExpectedSpeedup, testName);

        if (actualSpeedup < minimumSpeedup)
        {
            var environment = IsVirtualizedEnvironment() ? "(virtualized environment detected)" : "";
            throw new Xunit.Sdk.XunitException(
                $"Expected {testName} speedup > {minimumSpeedup:F1}x{environment}, but got {actualSpeedup:F2}x");
        }
    }

    /// <summary>
    /// Gets a performance tolerance factor based on the environment.
    /// </summary>
    public static double GetPerformanceTolerance()
    {
        if (IsVirtualizedEnvironment())
        {
            return 0.15; // 15% tolerance in virtual environments
        }

        if (Environment.GetEnvironmentVariable("CI") != null)
        {
            return 0.10; // 10% tolerance on CI
        }

        return 0.05; // 5% tolerance otherwise
    }
}
