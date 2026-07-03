// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Compilation;

/// <summary>
/// Cross-platform CUDA Toolkit include directories for NVRTC (<c>--include-path=...</c> options).
/// Previously the compilers hardcoded <c>/usr/local/cuda/include</c>, which does not exist on
/// Windows — harmless for the include-free generated kernels, but wrong for any kernel using
/// system headers (cooperative_groups.h, cuda::std::). Only directories that actually exist are
/// returned, so machines without a toolkit (e.g. natives-package deployments, GH #187) get none.
/// </summary>
internal static class NvrtcIncludePaths
{
    /// <summary>Builds the NVRTC <c>--include-path</c> options for the current machine.</summary>
    public static IReadOnlyList<string> GetIncludePathOptions()
    {
        var options = new List<string>();

        foreach (var root in GetToolkitRoots().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var include = Path.Combine(root, "include");
            if (Directory.Exists(include))
            {
                options.Add($"--include-path={include}");

                // CCCL (cuda::std::) headers, required by cooperative_groups on CUDA 11.1+.
                var cccl = Path.Combine(include, "cccl");
                if (Directory.Exists(cccl))
                {
                    options.Add($"--include-path={cccl}");
                }
            }

            // Linux toolkit layout keeps CCCL under targets/<arch>/include/cccl.
            var linuxCccl = Path.Combine(root, "targets", "x86_64-linux", "include", "cccl");
            if (Directory.Exists(linuxCccl))
            {
                options.Add($"--include-path={linuxCccl}");
            }

            if (options.Count > 0)
            {
                break; // first toolkit that provides headers wins (newest first on Windows)
            }
        }

        return options;
    }

    private static IEnumerable<string> GetToolkitRoots()
    {
        foreach (var variable in new[] { "CUDA_PATH", "CUDA_HOME" })
        {
            if (Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value)
            {
                yield return value;
            }
        }

        if (OperatingSystem.IsWindows())
        {
            const string DefaultToolkitRoot = @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA";
            if (Directory.Exists(DefaultToolkitRoot))
            {
                // Numeric version sort — lexicographic would rank v9.0 above v13.0.
                foreach (var dir in Directory.GetDirectories(DefaultToolkitRoot, "v*")
                    .OrderByDescending(d => Version.TryParse(Path.GetFileName(d).TrimStart('v', 'V'), out var v) ? v : new Version(0, 0)))
                {
                    yield return dir;
                }
            }
        }
        else
        {
            yield return "/usr/local/cuda";
            yield return "/opt/cuda";
        }
    }
}
