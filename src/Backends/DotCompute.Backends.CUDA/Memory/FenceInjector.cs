// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using System.Text.RegularExpressions;
using DotCompute.Abstractions.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Backends.CUDA.Memory;

/// <summary>
/// Handles PTX code injection for memory fences in CUDA kernels.
/// </summary>
/// <remarks>
/// <para>
/// This class injects fence instructions into compiled PTX code based on <see cref="FenceRequest"/>
/// specifications retrieved from <see cref="IFenceInjectionService"/>. The fence instructions
/// are injected at specified locations:
/// <list type="bullet">
/// <item><description><strong>AtEntry:</strong> Fence at kernel entry (after prologue)</description></item>
/// <item><description><strong>AtExit:</strong> Fence before ret instructions</description></item>
/// <item><description><strong>AfterWrites:</strong> Fence after store operations (st.*)</description></item>
/// <item><description><strong>BeforeReads:</strong> Fence before load operations (ld.*)</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>PTX Fence Instructions:</strong>
/// <list type="bullet">
/// <item><description><c>bar.sync 0;</c> - Thread-block scope synchronization barrier</description></item>
/// <item><description><c>membar.gl;</c> - Device/global memory barrier</description></item>
/// <item><description><c>membar.sys;</c> - System-wide memory barrier</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Performance Impact:</strong>
/// <list type="bullet">
/// <item><description>ThreadBlock fence: ~10ns</description></item>
/// <item><description>Device fence: ~100ns</description></item>
/// <item><description>System fence: ~200ns</description></item>
/// </list>
/// </para>
/// </remarks>
internal static partial class FenceInjector
{
    #region LoggerMessage Delegates

    [LoggerMessage(
        EventId = 9100,
        Level = LogLevel.Debug,
        Message = "Injecting {FenceCount} fence(s) into kernel '{KernelName}'")]
    private static partial void LogFenceInjectionStart(ILogger logger, int fenceCount, string kernelName);

    [LoggerMessage(
        EventId = 9101,
        Level = LogLevel.Debug,
        Message = "Fence injection complete for kernel '{KernelName}': {InjectedCount} fences injected")]
    private static partial void LogFenceInjectionComplete(ILogger logger, string kernelName, int injectedCount);

    [LoggerMessage(
        EventId = 9102,
        Level = LogLevel.Warning,
        Message = "Failed to inject fence into kernel '{KernelName}': {Reason}")]
    private static partial void LogFenceInjectionFailed(ILogger logger, string kernelName, string reason);

    [LoggerMessage(
        EventId = 9103,
        Level = LogLevel.Debug,
        Message = "Injecting {FenceType} fence at {Location} in kernel '{KernelName}'")]
    private static partial void LogFenceLocationInjection(ILogger logger, FenceType fenceType, string location, string kernelName);

    #endregion

    // Regex patterns for PTX parsing
    [GeneratedRegex(@"\.visible\s+\.entry\s+(\w+)\s*\(", RegexOptions.Multiline)]
    private static partial Regex EntryFunctionPattern();

    [GeneratedRegex(@"\{(\s*)", RegexOptions.Multiline)]
    private static partial Regex FunctionBodyStartPattern();

    [GeneratedRegex(@"(\s*)ret;", RegexOptions.Multiline)]
    private static partial Regex ReturnPattern();

    [GeneratedRegex(@"(\s*)(st\.\w+)", RegexOptions.Multiline)]
    private static partial Regex StorePattern();

    [GeneratedRegex(@"(\s*)(ld\.\w+)", RegexOptions.Multiline)]
    private static partial Regex LoadPattern();

    /// <summary>
    /// Injects memory fences into PTX bytecode based on pending fence requests.
    /// </summary>
    /// <param name="ptxBytes">Original PTX bytecode.</param>
    /// <param name="kernelName">Name of the kernel for logging and identification.</param>
    /// <param name="fenceService">Service providing pending fence requests.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <returns>Modified PTX bytecode with fence injection, or original if no fences or injection fails.</returns>
    public static byte[] InjectFencesIntoPtx(
        byte[] ptxBytes,
        string kernelName,
        IFenceInjectionService fenceService,
        ILogger? logger = null)
    {
        logger ??= NullLogger.Instance;

        var pendingFences = fenceService.GetPendingFences();
        if (pendingFences.Count == 0)
        {
            return ptxBytes;
        }

        LogFenceInjectionStart(logger, pendingFences.Count, kernelName);

        try
        {
            // Convert PTX bytes to string
            var ptxCode = Encoding.UTF8.GetString(ptxBytes);

            // Inject fences
            var (modifiedPtx, injectedCount) = InjectFences(ptxCode, kernelName, pendingFences, logger);

            if (injectedCount == 0)
            {
                LogFenceInjectionFailed(logger, kernelName, "No fences were injected");
                return ptxBytes;
            }

            // Convert back to bytes
            var modifiedBytes = Encoding.UTF8.GetBytes(modifiedPtx);

            LogFenceInjectionComplete(logger, kernelName, injectedCount);

            // Clear processed fences
            fenceService.ClearPendingFences();

            return modifiedBytes;
        }
        catch (Exception ex)
        {
            LogFenceInjectionFailed(logger, kernelName, ex.Message);
            return ptxBytes;
        }
    }

    /// <summary>
    /// Injects fences into PTX string based on fence requests.
    /// </summary>
    private static (string ModifiedPtx, int InjectedCount) InjectFences(
        string ptxCode,
        string kernelName,
        IReadOnlyList<FenceRequest> fences,
        ILogger logger)
    {
        var result = ptxCode;
        var injectedCount = 0;

        // Group fences by location
        var entryFences = fences.Where(f => f.Location.AtEntry).ToList();
        var exitFences = fences.Where(f => f.Location.AtExit).ToList();
        var afterWriteFences = fences.Where(f => f.Location.AfterWrites && !f.Location.AtEntry && !f.Location.AtExit).ToList();
        var beforeReadFences = fences.Where(f => f.Location.BeforeReads && !f.Location.AtEntry && !f.Location.AtExit).ToList();

        // Inject at kernel entry
        if (entryFences.Count > 0)
        {
            var (modified, success) = InjectAtEntry(result, kernelName, entryFences, logger);
            if (success)
            {
                result = modified;
                injectedCount += entryFences.Count;
            }
        }

        // Inject at kernel exit (before ret instructions)
        if (exitFences.Count > 0)
        {
            var (modified, count) = InjectAtExit(result, kernelName, exitFences, logger);
            result = modified;
            injectedCount += count;
        }

        // Inject after write operations (performance-sensitive, optional)
        if (afterWriteFences.Count > 0)
        {
            var (modified, count) = InjectAfterStores(result, kernelName, afterWriteFences, logger);
            result = modified;
            injectedCount += count;
        }

        // Inject before read operations (performance-sensitive, optional)
        if (beforeReadFences.Count > 0)
        {
            var (modified, count) = InjectBeforeLoads(result, kernelName, beforeReadFences, logger);
            result = modified;
            injectedCount += count;
        }

        return (result, injectedCount);
    }

    /// <summary>
    /// Injects fences at kernel entry point.
    /// </summary>
    private static (string ModifiedPtx, bool Success) InjectAtEntry(
        string ptxCode,
        string kernelName,
        List<FenceRequest> fences,
        ILogger logger)
    {
        // Find the .entry function declaration
        var entryMatch = EntryFunctionPattern().Match(ptxCode);
        if (!entryMatch.Success)
        {
            LogFenceInjectionFailed(logger, kernelName, "Could not find .entry function");
            return (ptxCode, false);
        }

        var entryIndex = entryMatch.Index;

        // Find the function body start (opening brace)
        var bodyStartMatch = FunctionBodyStartPattern().Match(ptxCode, entryIndex);
        if (!bodyStartMatch.Success)
        {
            LogFenceInjectionFailed(logger, kernelName, "Could not find function body start");
            return (ptxCode, false);
        }

        var bodyStartIndex = bodyStartMatch.Index + bodyStartMatch.Length;

        // Build fence code to inject
        var fenceCode = BuildFenceCode(fences, "entry", logger, kernelName);

        // Construct modified PTX
        var result = new StringBuilder(ptxCode.Length + fenceCode.Length);
        result.Append(ptxCode.AsSpan(0, bodyStartIndex));
        result.Append(fenceCode);
        result.Append(ptxCode.AsSpan(bodyStartIndex));

        foreach (var fence in fences)
        {
            LogFenceLocationInjection(logger, fence.Type, "entry", kernelName);
        }

        return (result.ToString(), true);
    }

    /// <summary>
    /// Injects fences before ret instructions (kernel exit).
    /// </summary>
    private static (string ModifiedPtx, int Count) InjectAtExit(
        string ptxCode,
        string kernelName,
        List<FenceRequest> fences,
        ILogger logger)
    {
        var fenceCode = BuildFenceCode(fences, "exit", logger, kernelName);

        // Replace all ret; instructions with fence + ret;
        var matches = ReturnPattern().Matches(ptxCode);
        if (matches.Count == 0)
        {
            return (ptxCode, 0);
        }

        var result = ReturnPattern().Replace(ptxCode, m =>
        {
            var whitespace = m.Groups[1].Value;
            return $"{whitespace}{fenceCode.TrimEnd()}\n{whitespace}ret;";
        });

        foreach (var fence in fences)
        {
            LogFenceLocationInjection(logger, fence.Type, "exit", kernelName);
        }

        return (result, matches.Count * fences.Count);
    }

    /// <summary>
    /// Injects fences after store operations.
    /// </summary>
    /// <remarks>
    /// This is a performance-sensitive operation. Use sparingly as it can significantly
    /// impact kernel performance when many stores are present.
    /// </remarks>
    private static (string ModifiedPtx, int Count) InjectAfterStores(
        string ptxCode,
        string kernelName,
        List<FenceRequest> fences,
        ILogger logger)
    {
        // Build compact fence instruction
        var fence = fences.First();
        var fenceInstruction = fence.GetPtxInstruction();

        var matches = StorePattern().Matches(ptxCode);
        if (matches.Count == 0)
        {
            return (ptxCode, 0);
        }

        // Find each store line and inject fence after
        var result = new StringBuilder();
        var lastEnd = 0;

        foreach (Match match in matches)
        {
            // Copy up to and including this line
            var lineEnd = ptxCode.IndexOf('\n', match.Index);
            if (lineEnd < 0)
            {
                lineEnd = ptxCode.Length;
            }

            result.Append(ptxCode.AsSpan(lastEnd, lineEnd - lastEnd + 1));

            // Add fence instruction with proper indentation
            var whitespace = match.Groups[1].Value;
            result.Append(whitespace).Append("    ").Append(fenceInstruction).Append("  // Injected: post-write fence\n");

            lastEnd = lineEnd + 1;
        }

        // Append remaining code
        if (lastEnd < ptxCode.Length)
        {
            result.Append(ptxCode.AsSpan(lastEnd));
        }

        LogFenceLocationInjection(logger, fence.Type, "after-stores", kernelName);

        return (result.ToString(), matches.Count);
    }

    /// <summary>
    /// Injects fences before load operations.
    /// </summary>
    /// <remarks>
    /// This is a performance-sensitive operation. Use sparingly as it can significantly
    /// impact kernel performance when many loads are present.
    /// </remarks>
    private static (string ModifiedPtx, int Count) InjectBeforeLoads(
        string ptxCode,
        string kernelName,
        List<FenceRequest> fences,
        ILogger logger)
    {
        // Build compact fence instruction
        var fence = fences.First();
        var fenceInstruction = fence.GetPtxInstruction();

        var matches = LoadPattern().Matches(ptxCode);
        if (matches.Count == 0)
        {
            return (ptxCode, 0);
        }

        // Find each load line and inject fence before
        var result = new StringBuilder();
        var lastEnd = 0;

        foreach (Match match in matches)
        {
            // Copy up to the load instruction (but not including it)
            result.Append(ptxCode.AsSpan(lastEnd, match.Index - lastEnd));

            // Add fence instruction with proper indentation
            var whitespace = match.Groups[1].Value;
            result.Append(whitespace).Append(fenceInstruction).Append("  // Injected: pre-read fence\n");

            lastEnd = match.Index;
        }

        // Append remaining code
        if (lastEnd < ptxCode.Length)
        {
            result.Append(ptxCode.AsSpan(lastEnd));
        }

        LogFenceLocationInjection(logger, fence.Type, "before-loads", kernelName);

        return (result.ToString(), matches.Count);
    }

    /// <summary>
    /// Builds PTX fence code block for injection.
    /// </summary>
    private static string BuildFenceCode(
        List<FenceRequest> fences,
        string location,
        ILogger logger,
        string kernelName)
    {
        var builder = new StringBuilder();
        builder.AppendLine();
        builder.AppendLine($"    // === Injected Memory Fences ({location}) (DotCompute) ===");

        foreach (var fence in fences)
        {
            var ptxInstruction = fence.GetPtxInstruction();
            var fenceTypeComment = fence.Type switch
            {
                FenceType.ThreadBlock => "Thread-block scope",
                FenceType.Device => "Device/global scope",
                FenceType.System => "System-wide scope",
                _ => "Memory fence"
            };

            builder.AppendLine($"    {ptxInstruction}  // {fenceTypeComment}");
        }

        builder.AppendLine($"    // === End Memory Fences ({location}) ===");
        builder.AppendLine();

        return builder.ToString();
    }

    /// <summary>
    /// Determines if fence injection is supported for the given compute capability.
    /// </summary>
    /// <param name="major">Major compute capability.</param>
    /// <param name="minor">Minor compute capability.</param>
    /// <returns>True if fence injection is supported; otherwise, false.</returns>
    public static bool IsFenceInjectionSupported(int major, int minor)
    {
        // Memory barriers require:
        // - CC 2.0+ for thread-block and device fences
        // - CC 2.0+ with UVA for system fences
        return major >= 2;
    }
}
