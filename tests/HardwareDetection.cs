using System.Runtime.InteropServices;
using System.Diagnostics;

namespace DotCompute.Tests.Common;

/// <summary>
/// Hardware detection utilities for conditional test execution
/// </summary>
public static class HardwareDetection
{
    private static bool? _cudaAvailable;
    private static bool? _openClAvailable;
    private static bool? _directComputeAvailable;
    private static bool? _metalAvailable;

    /// <summary>
    /// Detects if CUDA is available on the system
    /// </summary>
    public static bool IsCudaAvailable()
    {
        if (_cudaAvailable.HasValue)
            return _cudaAvailable.Value;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var result = WindowsCuda.CudaInit(0);
                _cudaAvailable = result == 0;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var result = LinuxCuda.CudaInit(0);
                _cudaAvailable = result == 0;
            }
            else
            {
                _cudaAvailable = false;
            }
        }
        catch
        {
            _cudaAvailable = false;
        }

        return _cudaAvailable.Value;
    }

    /// <summary>
    /// Detects if OpenCL is available on the system
    /// </summary>
    public static bool IsOpenCLAvailable()
    {
        if (_openClAvailable.HasValue)
            return _openClAvailable.Value;

        try
        {
            uint platformCount = 0;
            var result = clGetPlatformIDs(0, IntPtr.Zero, ref platformCount);
            _openClAvailable = result == 0 && platformCount > 0;
        }
        catch
        {
            _openClAvailable = false;
        }

        return _openClAvailable.Value;
    }

    /// <summary>
    /// Detects if DirectCompute/Direct3D is available on Windows
    /// </summary>
    public static bool IsDirectComputeAvailable()
    {
        if (_directComputeAvailable.HasValue)
            return _directComputeAvailable.Value;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _directComputeAvailable = false;
            return false;
        }

        try
        {
            // Check for DirectX/Direct3D availability via dxdiag
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "dxdiag",
                Arguments = "/t temp_dxdiag.txt",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            });

            process?.WaitForExit(5000);
            _directComputeAvailable = process?.ExitCode == 0;

            // Clean up temp file
            if (File.Exists("temp_dxdiag.txt"))
                File.Delete("temp_dxdiag.txt");
        }
        catch
        {
            _directComputeAvailable = false;
        }

        return _directComputeAvailable.Value;
    }

    /// <summary>
    /// Detects if Metal is available on macOS
    /// </summary>
    public static bool IsMetalAvailable()
    {
        if (_metalAvailable.HasValue)
            return _metalAvailable.Value;

        _metalAvailable = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        return _metalAvailable.Value;
    }

    /// <summary>
    /// Gets the reason why hardware is not available
    /// </summary>
    public static string GetUnavailabilityReason(string hardwareType)
    {
        return hardwareType.ToLowerInvariant() switch
        {
            "cuda" => IsCudaAvailable() ? "CUDA is available" : "CUDA runtime not installed or no NVIDIA GPU present",
            "opencl" => IsOpenCLAvailable() ? "OpenCL is available" : "OpenCL runtime not installed or no compatible devices",
            "directcompute" => IsDirectComputeAvailable() ? "DirectCompute is available" : "DirectCompute not available or not on Windows",
            "metal" => IsMetalAvailable() ? "Metal is available" : "Metal only available on macOS",
            _ => $"Unknown hardware type: {hardwareType}"
        };
    }

    /// <summary>
    /// Checks if running in a CI environment
    /// </summary>
    public static bool IsCI()
    {
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")) ||
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")) ||
               !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_HTTP_USER_AGENT"));
    }

    #region Native P/Invoke declarations

    private static class WindowsCuda
    {
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport("nvcuda.dll", EntryPoint = "cuInit")]
        public static extern int CudaInit(uint flags);
    }

    private static class LinuxCuda
    {
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport("libcuda.so.1", EntryPoint = "cuInit")]
        public static extern int CudaInit(uint flags);
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("opencl", EntryPoint = "clGetPlatformIDs")]
    private static extern int clGetPlatformIDs(uint num_entries, IntPtr platforms, ref uint num_platforms);

    #endregion
}