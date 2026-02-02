// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.RingKernels;

/// <summary>
/// Base class for ring kernel stub generators providing common utility methods
/// and pipeline structure. Eliminates ~50-60 lines of duplicate code across
/// CUDA and Metal implementations.
/// </summary>
/// <typeparam name="TKernel">The discovered kernel type (e.g., DiscoveredRingKernel, DiscoveredMetalRingKernel).</typeparam>
public abstract class RingKernelStubGeneratorBase<TKernel>
    where TKernel : class
{
    /// <summary>
    /// Logger instance for diagnostic output.
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// Gets the backend name for this generator (e.g., "CUDA", "Metal").
    /// </summary>
    protected abstract string BackendName { get; }

    /// <summary>
    /// Initializes a new instance of the stub generator.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    protected RingKernelStubGeneratorBase(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        Logger = logger;
    }

    /// <summary>
    /// Generates a kernel stub for the specified kernel.
    /// Template method that orchestrates the generation pipeline.
    /// </summary>
    /// <param name="kernel">The discovered kernel metadata.</param>
    /// <param name="includeHostLauncher">Whether to include host launcher code.</param>
    /// <returns>Generated source code string.</returns>
    public virtual string GenerateKernelStub(TKernel kernel, bool includeHostLauncher = false)
    {
        ArgumentNullException.ThrowIfNull(kernel);

        var kernelId = GetKernelId(kernel);
        Logger.LogInformation("Generating {Backend} ring kernel stub for '{KernelId}'", BackendName, kernelId);

        try
        {
            var sourceBuilder = new StringBuilder(4096);

            // Pipeline stages - derived classes implement specific behavior
            AppendFileHeader(sourceBuilder, kernel);
            AppendIncludes(sourceBuilder, kernel);
            AppendStructDefinitions(sourceBuilder, kernel);

            if (UsesK2KMessaging(kernel))
            {
                AppendK2KMessagingInfrastructure(sourceBuilder, kernel);
            }

            AppendHandlerFunction(sourceBuilder, kernel);
            AppendKernelSignature(sourceBuilder, kernel);
            AppendKernelBody(sourceBuilder, kernel);

            if (includeHostLauncher)
            {
                AppendLaunchWrapper(sourceBuilder, kernel);
            }

            var lineCount = sourceBuilder.ToString().Split('\n').Length;
            Logger.LogInformation("Generated {Backend} kernel stub for '{KernelId}': {LineCount} lines",
                BackendName, kernelId, lineCount);

            return sourceBuilder.ToString();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to generate {Backend} kernel stub for '{KernelId}'",
                BackendName, kernelId);
            throw;
        }
    }

    /// <summary>
    /// Generates batch kernel stubs for multiple kernels.
    /// </summary>
    /// <param name="kernels">The kernels to generate stubs for.</param>
    /// <param name="unitName">Name of the compilation unit.</param>
    /// <param name="includeHostLaunchers">Whether to include host launcher code.</param>
    /// <returns>Generated source code string.</returns>
    public virtual string GenerateBatchKernelStubs(
        IEnumerable<TKernel> kernels,
        string unitName,
        bool includeHostLaunchers = false)
    {
        ArgumentNullException.ThrowIfNull(kernels);
        ArgumentException.ThrowIfNullOrWhiteSpace(unitName);

        var kernelList = kernels.ToList();
        if (kernelList.Count == 0)
        {
            return string.Empty;
        }

        Logger.LogInformation("Generating batch {Backend} kernel stubs for unit '{UnitName}' with {KernelCount} kernels",
            BackendName, unitName, kernelList.Count);

        try
        {
            var sourceBuilder = new StringBuilder(8192);

            // Batch header
            AppendBatchHeader(sourceBuilder, unitName, kernelList.Count);

            // Common includes for all kernels
            AppendBatchIncludes(sourceBuilder, kernelList);

            // Analyze for K2K usage
            var hasK2K = kernelList.Any(UsesK2KMessaging);

            // Common struct definitions
            AppendStructDefinitions(sourceBuilder, kernelList.FirstOrDefault());

            if (hasK2K)
            {
                AppendK2KMessagingInfrastructure(sourceBuilder, kernelList.FirstOrDefault());
            }

            // Generate each kernel
            foreach (var kernel in kernelList)
            {
                var kernelId = GetKernelId(kernel);

                sourceBuilder.AppendLine();
                sourceBuilder.AppendLine("// ============================================================================");
                sourceBuilder.AppendLine(CultureInfo.InvariantCulture, $"// Ring Kernel: {kernelId}");
                sourceBuilder.AppendLine("// ============================================================================");
                sourceBuilder.AppendLine();

                AppendHandlerFunction(sourceBuilder, kernel);
                AppendKernelSignature(sourceBuilder, kernel);
                AppendKernelBody(sourceBuilder, kernel);

                if (includeHostLaunchers)
                {
                    AppendLaunchWrapper(sourceBuilder, kernel);
                }
            }

            var lineCount = sourceBuilder.ToString().Split('\n').Length;
            Logger.LogInformation("Generated batch {Backend} kernel stubs for unit '{UnitName}': {LineCount} lines, {KernelCount} kernels",
                BackendName, unitName, lineCount, kernelList.Count);

            return sourceBuilder.ToString();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to generate batch {Backend} kernel stubs for unit '{UnitName}'",
                BackendName, unitName);
            throw;
        }
    }

    /// <summary>
    /// Appends the standard batch header to the source builder.
    /// </summary>
    protected virtual void AppendBatchHeader(StringBuilder builder, string unitName, int kernelCount)
    {
        builder.AppendLine("// ===========================================================================");
        builder.AppendLine(CultureInfo.InvariantCulture, $"// Auto-Generated {BackendName} Ring Kernel Batch Compilation Unit");
        builder.AppendLine(CultureInfo.InvariantCulture, $"// Unit Name: {unitName}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"// Kernel Count: {kernelCount}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"// Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        builder.AppendLine("// DO NOT EDIT - This file is automatically generated");
        builder.AppendLine("// ===========================================================================");
        builder.AppendLine();
    }

    /// <summary>
    /// Converts a PascalCase string to snake_case.
    /// </summary>
    /// <param name="str">The input string in PascalCase.</param>
    /// <returns>The string converted to snake_case.</returns>
    protected static string ToSnakeCase(string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return str;
        }

        var result = new StringBuilder(str.Length + 10);
        for (var i = 0; i < str.Length; i++)
        {
            var c = str[i];
            if (char.IsUpper(c) && i > 0 && (i + 1 < str.Length && char.IsLower(str[i + 1]) || char.IsLower(str[i - 1])))
            {
                result.Append('_');
                result.Append(char.ToLowerInvariant(c));
            }
            else
            {
                result.Append(char.ToLowerInvariant(c));
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Gets the handler function name from a method name.
    /// </summary>
    /// <param name="methodName">The method name.</param>
    /// <returns>The handler function name in snake_case with "process_" prefix.</returns>
    protected static string GetHandlerFunctionName(string methodName)
    {
        var baseName = methodName
            .Replace("RingKernel", "", StringComparison.Ordinal)
            .Replace("Kernel", "", StringComparison.Ordinal);
        return $"process_{ToSnakeCase(baseName)}_message";
    }

    // ===== Abstract methods that derived classes must implement =====

    /// <summary>
    /// Gets the kernel ID from the kernel metadata.
    /// </summary>
    protected abstract string GetKernelId(TKernel kernel);

    /// <summary>
    /// Returns whether the kernel uses K2K messaging.
    /// </summary>
    protected abstract bool UsesK2KMessaging(TKernel kernel);

    /// <summary>
    /// Appends the file header for a single kernel.
    /// </summary>
    protected abstract void AppendFileHeader(StringBuilder builder, TKernel kernel);

    /// <summary>
    /// Appends include directives for a single kernel.
    /// </summary>
    protected abstract void AppendIncludes(StringBuilder builder, TKernel kernel);

    /// <summary>
    /// Appends include directives for a batch of kernels.
    /// </summary>
    protected abstract void AppendBatchIncludes(StringBuilder builder, IReadOnlyList<TKernel> kernels);

    /// <summary>
    /// Appends struct definitions.
    /// </summary>
    protected abstract void AppendStructDefinitions(StringBuilder builder, TKernel? kernel);

    /// <summary>
    /// Appends K2K messaging infrastructure.
    /// </summary>
    protected abstract void AppendK2KMessagingInfrastructure(StringBuilder builder, TKernel? kernel);

    /// <summary>
    /// Appends the message handler function.
    /// </summary>
    protected abstract void AppendHandlerFunction(StringBuilder builder, TKernel kernel);

    /// <summary>
    /// Appends the kernel signature.
    /// </summary>
    protected abstract void AppendKernelSignature(StringBuilder builder, TKernel kernel);

    /// <summary>
    /// Appends the kernel body implementation.
    /// </summary>
    protected abstract void AppendKernelBody(StringBuilder builder, TKernel kernel);

    /// <summary>
    /// Appends the host launch wrapper (optional).
    /// </summary>
    protected virtual void AppendLaunchWrapper(StringBuilder builder, TKernel kernel)
    {
        // Default: no-op. Override in derived classes if needed.
    }
}
