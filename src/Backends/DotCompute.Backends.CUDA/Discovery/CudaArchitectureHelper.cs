// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.CUDA.Discovery;

/// <summary>
/// Provides architecture detection and capability mapping for CUDA devices.
/// Centralizes architecture-specific logic for compute capability determination.
/// </summary>
public static class CudaArchitectureHelper
{
    /// <summary>
    /// Gets the architecture generation name based on compute capability.
    /// </summary>
    /// <param name="major">Compute capability major version.</param>
    /// <param name="minor">Compute capability minor version.</param>
    /// <returns>The architecture generation name.</returns>
    public static string GetArchitectureGeneration(int major, int minor)
    {
        return major switch
        {
            // Only CUDA 13.0+ supported architectures
            7 when minor == 5 => "Turing",
            8 when minor == 0 => "Ampere (GA100)",
            8 when minor == 6 => "Ampere (GA10x)",
            8 when minor == 7 => "Ampere (GA10x)",
            8 when minor == 9 => "Ada Lovelace",
            9 when minor == 0 => "Hopper",
            _ => $"Unknown (CC {major}.{minor})"
        };
    }

    /// <summary>
    /// Gets the CUDA cores per streaming multiprocessor for the given architecture.
    /// </summary>
    /// <param name="major">Compute capability major version.</param>
    /// <param name="minor">Compute capability minor version.</param>
    /// <returns>The number of CUDA cores per SM.</returns>
    public static int GetCudaCoresPerSM(int major, int minor)
    {
        return major switch
        {
            7 when minor >= 5 => 64,  // Turing (sm_75)
            8 when minor == 0 => 64,  // Ampere GA100 (sm_80)
            8 when minor == 6 => 128, // Ampere GA10x (sm_86)
            8 when minor == 7 => 128, // Ampere GA10x (sm_87)
            8 when minor == 9 => 128, // Ada Lovelace (sm_89)
            9 when minor == 0 => 128, // Hopper (sm_90)
            _ => 128  // Default for future architectures
        };
    }

    /// <summary>
    /// Gets the Tensor Cores per streaming multiprocessor for the given architecture.
    /// </summary>
    /// <param name="major">Compute capability major version.</param>
    /// <param name="minor">Compute capability minor version.</param>
    /// <returns>The number of Tensor Cores per SM.</returns>
    public static int GetTensorCoresPerSM(int major, int minor)
    {
        return major switch
        {
            7 when minor >= 0 => 8,  // Volta/Turing: 8 Tensor Cores per SM
            8 when minor >= 0 => 4,  // Ampere: 4 3rd gen Tensor Cores per SM
            9 when minor >= 0 => 4,  // Hopper: 4 4th gen Tensor Cores per SM
            _ => 0  // No Tensor Cores for older architectures
        };
    }

    /// <summary>
    /// Checks if the compute capability is compatible with CUDA 13.0.
    /// </summary>
    /// <param name="major">Compute capability major version.</param>
    /// <param name="minor">Compute capability minor version.</param>
    /// <returns>True if compatible with CUDA 13.0.</returns>
    public static bool IsCuda13Compatible(int major, int minor)
    {
        // CUDA 13.0 drops support for Maxwell (sm_5x), Pascal (sm_6x), and Volta (sm_70, sm_72)
        // Minimum supported: Turing (sm_75) and newer
        return major > 7 || (major == 7 && minor >= 5);
    }

    /// <summary>
    /// Checks if the compute capability supports the specified capability level.
    /// </summary>
    /// <param name="deviceMajor">Device compute capability major version.</param>
    /// <param name="deviceMinor">Device compute capability minor version.</param>
    /// <param name="requiredMajor">Required compute capability major version.</param>
    /// <param name="requiredMinor">Required compute capability minor version.</param>
    /// <returns>True if the device supports the required capability or higher.</returns>
    public static bool SupportsComputeCapability(
        int deviceMajor, int deviceMinor,
        int requiredMajor, int requiredMinor)
    {
        return deviceMajor > requiredMajor ||
               (deviceMajor == requiredMajor && deviceMinor >= requiredMinor);
    }

    /// <summary>
    /// Detects if a device is an RTX 2000 Ada Generation GPU.
    /// </summary>
    /// <param name="major">Compute capability major version.</param>
    /// <param name="minor">Compute capability minor version.</param>
    /// <param name="deviceName">The device name.</param>
    /// <returns>True if the device is an RTX 2000 Ada GPU.</returns>
    public static bool IsRTX2000Ada(int major, int minor, string deviceName)
    {
        var isAdaLovelace = major == 8 && minor == 9;
        var nameContainsRTX2000 = deviceName.Contains("RTX 2000", StringComparison.OrdinalIgnoreCase) ||
                                  deviceName.Contains("RTX A2000", StringComparison.OrdinalIgnoreCase) ||
                                  deviceName.Contains("RTX 2000 Ada", StringComparison.OrdinalIgnoreCase);

        return isAdaLovelace && nameContainsRTX2000;
    }

    /// <summary>
    /// Gets the SM architecture code (e.g., "sm_89" for Ada Lovelace).
    /// </summary>
    /// <param name="major">Compute capability major version.</param>
    /// <param name="minor">Compute capability minor version.</param>
    /// <returns>The SM architecture code string.</returns>
    public static string GetSMArchitecture(int major, int minor)
    {
        return $"sm_{major}{minor}";
    }

    /// <summary>
    /// Gets the feature support flags for a given compute capability.
    /// </summary>
    /// <param name="major">Compute capability major version.</param>
    /// <param name="minor">Compute capability minor version.</param>
    /// <returns>A dictionary of feature names and their support status.</returns>
    public static IReadOnlyDictionary<string, bool> GetFeatureSupport(int major, int minor)
    {
        return new Dictionary<string, bool>
        {
            ["TensorCores"] = major >= 7,
            ["BFloat16"] = major >= 8,
            ["CooperativeGroups"] = major >= 6,
            ["DynamicParallelism"] = major >= 3 && minor >= 5,
            ["SharedMemoryRegisterSpilling"] = major >= 7 && minor >= 5,
            ["TileBasedProgramming"] = major >= 8,
            ["AsyncCopyOperations"] = major >= 8,
            ["L2CacheResidencyControl"] = major >= 8,
            ["GraphOptimizationV2"] = major >= 8,
            ["Int4TensorCores"] = major >= 8 && minor >= 9,
            ["FP8TensorCores"] = major >= 9,
            ["IsCuda13Compatible"] = IsCuda13Compatible(major, minor)
        };
    }
}
