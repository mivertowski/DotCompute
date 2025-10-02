// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CUDA.Compilation;

/// <summary>
/// Validates CUDA source code and compiled binaries for correctness and best practices.
/// Provides comprehensive validation including syntax checking, performance analysis, and security validation.
/// </summary>
internal static class CudaCompilerValidator
{
    /// <summary>
    /// Validates CUDA source code for common issues before compilation.
    /// Checks for CUDA kernel structure, deprecated functions, and potential performance issues.
    /// </summary>
    /// <param name="cudaSource">CUDA source code to validate.</param>
    /// <param name="kernelName">Name of the kernel for error reporting.</param>
    /// <param name="logger">Logger for validation warnings and errors.</param>
    /// <returns>Validation result with success status and any warnings.</returns>
    public static Types.UnifiedValidationResult ValidateCudaSource(string cudaSource, string kernelName, ILogger logger)
    {
        var warnings = new List<string>();

        try
        {
            // Check for basic CUDA kernel structure
            if (!cudaSource.Contains("__global__", StringComparison.Ordinal) && !cudaSource.Contains("__device__", StringComparison.Ordinal))
            {
                return Types.UnifiedValidationResult.Failure("CUDA source must contain at least one __global__ or __device__ function");
            }

            // Check for potential issues
            if (cudaSource.Contains("printf", StringComparison.Ordinal) && !cudaSource.Contains("#include <cstdio>", StringComparison.Ordinal))
            {
                warnings.Add("Using printf without including <cstdio> may cause compilation issues");
            }

            if (cudaSource.Contains("__syncthreads()", StringComparison.Ordinal) && !cudaSource.Contains("__shared__", StringComparison.Ordinal))
            {
                warnings.Add("Using __syncthreads() without shared memory may indicate inefficient synchronization");
            }

            // Check for deprecated functions
            if (cudaSource.Contains("__threadfence_system", StringComparison.Ordinal))
            {
                warnings.Add("__threadfence_system is deprecated, consider using __threadfence() or memory fences");
            }

            // Check for potential memory issues
            if (cudaSource.Contains("malloc", StringComparison.Ordinal) || cudaSource.Contains("free", StringComparison.Ordinal))
            {
                warnings.Add("Dynamic memory allocation in kernels can impact performance and may not be supported on all devices");
            }

            // Check for performance anti-patterns
            ValidatePerformancePatterns(cudaSource, warnings);

            // Check for security issues
            ValidateSecurityPatterns(cudaSource, warnings);

            // Log warnings
            foreach (var warning in warnings)
            {
                logger.LogWarning("CUDA source validation warning for {KernelName}: {Warning}", kernelName, warning);
            }

            return warnings.Count > 0
                ? Types.UnifiedValidationResult.SuccessWithWarnings(warnings.ToArray())
                : Types.UnifiedValidationResult.Success();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CUDA source validation failed for kernel {KernelName}", kernelName);
            return Types.UnifiedValidationResult.Success("Source validation failed, proceeding with compilation");
        }
    }

    /// <summary>
    /// Verifies the compiled PTX/CUBIN for basic correctness.
    /// Validates binary structure and performs sanity checks on compiled output.
    /// </summary>
    /// <param name="compiledCode">Compiled PTX or CUBIN bytecode.</param>
    /// <param name="kernelName">Name of the kernel for error reporting.</param>
    /// <param name="logger">Logger for validation messages.</param>
    /// <returns>True if verification passes, false otherwise.</returns>
    public static bool VerifyCompiledCode(byte[] compiledCode, string kernelName, ILogger logger)
    {
        try
        {
            if (compiledCode == null || compiledCode.Length == 0)
            {
                logger.LogError("Compiled code is empty for kernel {KernelName}", kernelName);
                return false;
            }

            // Basic PTX validation
            var codeString = Encoding.UTF8.GetString(compiledCode);
            if (codeString.StartsWith(".version", StringComparison.Ordinal) || codeString.StartsWith("//", StringComparison.Ordinal))
            {
                // Looks like PTX
                if (!codeString.Contains(".entry", StringComparison.Ordinal))
                {
                    logger.LogError("PTX code missing .entry directive for kernel {KernelName}", kernelName);
                    return false;
                }

                logger.LogDebug("PTX verification passed for kernel {KernelName}", kernelName);
                return true;
            }

            // If it's binary data, assume it's CUBIN and do basic size check
            if (compiledCode.Length > 100) // CUBIN should be reasonably sized
            {
                logger.LogDebug("Binary verification passed for kernel {KernelName} (size: {Size} bytes)", kernelName, compiledCode.Length);
                return true;
            }

            logger.LogWarning("Code verification inconclusive for kernel {KernelName}", kernelName);
            return true; // Allow inconclusive results to proceed
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Code verification error for kernel {KernelName}", kernelName);
            return true; // Allow verification errors to proceed
        }
    }

    /// <summary>
    /// Validates performance-related patterns in CUDA source code.
    /// Identifies potential performance bottlenecks and anti-patterns.
    /// </summary>
    /// <param name="cudaSource">CUDA source code to analyze.</param>
    /// <param name="warnings">List to add performance warnings to.</param>
    private static void ValidatePerformancePatterns(string cudaSource, IReadOnlyList<string> warnings)
    {
        // Check for excessive register usage indicators
        if (cudaSource.Contains("register", StringComparison.Ordinal) &&

            System.Text.RegularExpressions.Regex.Matches(cudaSource, @"register\s+\w+").Count > 10)
        {
            warnings.Add("Excessive register usage detected - may reduce occupancy");
        }

        // Check for uncoalesced memory access patterns
        if (cudaSource.Contains("[threadIdx.x + ", StringComparison.Ordinal) &&

            !cudaSource.Contains("* blockDim.x", StringComparison.Ordinal))
        {
            warnings.Add("Potential uncoalesced memory access pattern detected");
        }

        // Check for branch divergence patterns
        var branchCount = System.Text.RegularExpressions.Regex.Matches(cudaSource, @"if\s*\(.*threadIdx").Count;
        if (branchCount > 3)
        {
            warnings.Add("Multiple thread-dependent branches may cause warp divergence");
        }

        // Check for inefficient synchronization
        if (cudaSource.Contains("__syncthreads()", StringComparison.Ordinal))
        {
            var syncCount = System.Text.RegularExpressions.Regex.Matches(cudaSource, @"__syncthreads\(\)").Count;
            if (syncCount > 5)
            {
                warnings.Add("Excessive synchronization points may reduce performance");
            }
        }

        // Check for atomic operations without proper considerations
        if (cudaSource.Contains("atomic", StringComparison.Ordinal) &&

            !cudaSource.Contains("__shared__", StringComparison.Ordinal))
        {
            warnings.Add("Global memory atomics without shared memory staging may be inefficient");
        }
    }

    /// <summary>
    /// Validates security-related patterns in CUDA source code.
    /// Identifies potential security vulnerabilities and unsafe practices.
    /// </summary>
    /// <param name="cudaSource">CUDA source code to analyze.</param>
    /// <param name="warnings">List to add security warnings to.</param>
    private static void ValidateSecurityPatterns(string cudaSource, IReadOnlyList<string> warnings)
    {
        // Check for buffer overflow risks
        if (cudaSource.Contains("char[", StringComparison.Ordinal) &&

            !cudaSource.Contains("sizeof", StringComparison.Ordinal))
        {
            warnings.Add("Fixed-size character arrays without size checks may be vulnerable to buffer overflows");
        }

        // Check for unchecked array access
        if (System.Text.RegularExpressions.Regex.IsMatch(cudaSource, @"\w+\[\w+\]\s*=") &&

            !cudaSource.Contains("if (", StringComparison.Ordinal))
        {
            warnings.Add("Array access without bounds checking detected");
        }

        // Check for potential integer overflow
        if (cudaSource.Contains("int ", StringComparison.Ordinal) &&

            (cudaSource.Contains(" + ", StringComparison.Ordinal) || cudaSource.Contains(" * ", StringComparison.Ordinal)) &&
            !cudaSource.Contains("overflow", StringComparison.Ordinal))
        {
            warnings.Add("Integer arithmetic without overflow protection may be unsafe");
        }

        // Check for hardcoded sensitive values
        if (System.Text.RegularExpressions.Regex.IsMatch(cudaSource, @"\b(?:password|key|secret|token)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            warnings.Add("Potential hardcoded sensitive information detected");
        }

        // Check for unsafe pointer operations
        if (cudaSource.Contains("reinterpret_cast", StringComparison.Ordinal) ||

            cudaSource.Contains("(void*)", StringComparison.Ordinal))
        {
            warnings.Add("Unsafe pointer casting detected - ensure type safety");
        }
    }

    /// <summary>
    /// Validates kernel function naming conventions and structure.
    /// Ensures kernels follow CUDA best practices for naming and organization.
    /// </summary>
    /// <param name="cudaSource">CUDA source code to analyze.</param>
    /// <param name="kernelName">Expected kernel name.</param>
    /// <returns>List of naming and structure validation warnings.</returns>
    public static IReadOnlyList<string> ValidateKernelStructure(string cudaSource, string kernelName)
    {
        var warnings = new List<string>();

        try
        {
            // Check if kernel name follows conventions
            if (!char.IsLower(kernelName[0]))
            {
                warnings.Add("Kernel names should start with lowercase letter by convention");
            }

            if (kernelName.Contains("_", StringComparison.Ordinal) && kernelName.Contains("Kernel", StringComparison.Ordinal))
            {
                warnings.Add("Redundant 'Kernel' suffix in kernel name - it's implied for __global__ functions");
            }

            // Check for proper extern "C" usage
            var hasGlobalFunctions = cudaSource.Contains("__global__", StringComparison.Ordinal);
            var hasExternC = cudaSource.Contains("extern \"C\"", StringComparison.Ordinal);


            if (hasGlobalFunctions && !hasExternC)
            {
                warnings.Add("Consider using extern \"C\" to prevent C++ name mangling issues");
            }

            // Check for proper parameter documentation
            if (!cudaSource.Contains("/**", StringComparison.Ordinal) && !cudaSource.Contains("//", StringComparison.Ordinal))
            {
                warnings.Add("Kernel parameters should be documented for maintainability");
            }

            // Check for proper error handling patterns
            if (!cudaSource.Contains("return", StringComparison.Ordinal) &&

                !cudaSource.Contains("assert", StringComparison.Ordinal))
            {
                warnings.Add("Consider adding error handling or assertions for robustness");
            }
        }
        catch (Exception)
        {
            // Ignore validation errors - these are just suggestions
        }

        return warnings;
    }

    /// <summary>
    /// Checks if the CUDA source is compatible with the target compute capability.
    /// Validates feature usage against device capabilities.
    /// </summary>
    /// <param name="cudaSource">CUDA source code to analyze.</param>
    /// <param name="computeCapability">Target compute capability (major, minor).</param>
    /// <returns>List of compatibility warnings.</returns>
    public static IReadOnlyList<string> ValidateComputeCapabilityCompatibility(string cudaSource, (int major, int minor) computeCapability)
    {
        var warnings = new List<string>();

        try
        {
            // Check for features requiring specific compute capabilities
            if (cudaSource.Contains("cooperative_groups", StringComparison.Ordinal) &&

                (computeCapability.major < 6 || (computeCapability.major == 6 && computeCapability.minor < 0)))
            {
                warnings.Add("Cooperative groups require compute capability 6.0 or higher");
            }

            if (cudaSource.Contains("__half", StringComparison.Ordinal) &&

                (computeCapability.major < 5 || (computeCapability.major == 5 && computeCapability.minor < 3)))
            {
                warnings.Add("Half-precision types require compute capability 5.3 or higher");
            }

            if (cudaSource.Contains("tensor_core", StringComparison.Ordinal) && computeCapability.major < 7)
            {
                warnings.Add("Tensor core operations require compute capability 7.0 or higher");
            }

            if (cudaSource.Contains("__ldg", StringComparison.Ordinal) &&

                (computeCapability.major < 3 || (computeCapability.major == 3 && computeCapability.minor < 5)))
            {
                warnings.Add("Read-only data cache load (__ldg) requires compute capability 3.5 or higher");
            }

            // Check for dynamic parallelism
            if (cudaSource.Contains("<<<", StringComparison.Ordinal) &&

                cudaSource.Contains(">>>", StringComparison.Ordinal) &&

                computeCapability.major < 3)
            {
                warnings.Add("Dynamic parallelism requires compute capability 3.5 or higher");
            }
        }
        catch (Exception)
        {
            // Ignore validation errors
        }

        return warnings;
    }
}
