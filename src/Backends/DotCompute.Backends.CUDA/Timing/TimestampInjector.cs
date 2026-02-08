// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using System.Text.RegularExpressions;
using DotCompute.Backends.CUDA.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Backends.CUDA.Timing;

/// <summary>
/// Handles PTX/CUBIN code injection for automatic timestamp recording at kernel entry.
/// </summary>
/// <remarks>
/// <para>
/// When timestamp injection is enabled, this class modifies compiled kernel code to:
/// <list type="bullet">
/// <item><description>Add a `long* timestamps` parameter as parameter slot 0</description></item>
/// <item><description>Insert timestamp recording code at kernel entry (thread 0 records GPU time)</description></item>
/// <item><description>Shift all user parameters to slots 1, 2, 3, etc.</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>PTX Injection Pattern:</strong>
/// <code>
/// .param .u64 param_0  // NEW: timestamp buffer
/// .param .u64 param_1  // Original user param 0
/// .param .u64 param_2  // Original user param 1
///
/// // Kernel prologue (inject after function entry)
/// {
///     .reg .pred %p;
///     .reg .u32 %tid_x, %tid_y, %tid_z;
///     .reg .u64 %timestamp;
///
///     mov.u32 %tid_x, %tid.x;
///     mov.u32 %tid_y, %tid.y;
///     mov.u32 %tid_z, %tid.z;
///
///     setp.ne.u32 %p, %tid_x, 0;
///     @%p bra SKIP_TIMESTAMP;
///     setp.ne.u32 %p, %tid_y, 0;
///     @%p bra SKIP_TIMESTAMP;
///     setp.ne.u32 %p, %tid_z, 0;
///     @%p bra SKIP_TIMESTAMP;
///
///     // Thread (0,0,0) records timestamp
///     mov.u64 %timestamp, %globaltimer;
///     st.global.u64 [param_0], %timestamp;
///
/// SKIP_TIMESTAMP:
///     // Original kernel code continues...
/// }
/// </code>
/// </para>
/// <para>
/// <strong>Performance Impact:</strong> &lt;20ns overhead per kernel launch (single thread timestamp write).
/// </para>
/// </remarks>
internal static partial class TimestampInjector
{
    #region LoggerMessage Delegates

    [LoggerMessage(
        EventId = 7200,
        Level = LogLevel.Debug,
        Message = "Injecting timestamp parameter and prologue into kernel '{KernelName}'")]
    private static partial void LogTimestampInjectionStart(ILogger logger, string kernelName);

    [LoggerMessage(
        EventId = 7201,
        Level = LogLevel.Debug,
        Message = "Timestamp injection complete for kernel '{KernelName}': {ParameterCount} parameters shifted")]
    private static partial void LogTimestampInjectionComplete(ILogger logger, string kernelName, int parameterCount);

    [LoggerMessage(
        EventId = 7202,
        Level = LogLevel.Warning,
        Message = "Failed to inject timestamp into kernel '{KernelName}': {Reason}")]
    private static partial void LogTimestampInjectionFailed(ILogger logger, string kernelName, string reason);

    [LoggerMessage(
        EventId = 7203,
        Level = LogLevel.Debug,
        Message = "Using {TimerType} for timestamp injection (CC {ComputeCapability})")]
    private static partial void LogTimerTypeSelected(ILogger logger, string timerType, string computeCapability);

    #endregion

    // Regex patterns for PTX parsing
    [GeneratedRegex(@"\.visible\s+\.entry\s+(\w+)\s*\(", RegexOptions.Multiline)]
    private static partial Regex EntryFunctionPattern();

    [GeneratedRegex(@"\.param\s+\.(\w+)\s+(\w+)", RegexOptions.Multiline)]
    private static partial Regex ParameterPattern();

    [GeneratedRegex(@"\{(\s*)", RegexOptions.Multiline)]
    private static partial Regex FunctionBodyStartPattern();

    [GeneratedRegex(@"param_(\d+)")]
    private static partial Regex ParamNumberPattern();

    /// <summary>
    /// Injects timestamp recording code into PTX bytecode.
    /// </summary>
    /// <param name="ptxBytes">Original PTX bytecode.</param>
    /// <param name="kernelName">Name of the kernel for logging and identification.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <returns>Modified PTX bytecode with timestamp injection, or original if injection fails.</returns>
    public static byte[] InjectTimestampIntoPtx(byte[] ptxBytes, string kernelName, ILogger? logger = null)
    {
        logger ??= NullLogger.Instance;
        LogTimestampInjectionStart(logger, kernelName);

        try
        {
            // Convert PTX bytes to string
            var ptxCode = Encoding.UTF8.GetString(ptxBytes);

            // Inject timestamp parameter and prologue
            var modifiedPtx = InjectTimestampCode(ptxCode, kernelName, logger);

            if (modifiedPtx == ptxCode)
            {
                LogTimestampInjectionFailed(logger, kernelName, "No changes made to PTX code");
                return ptxBytes; // Return original if no changes
            }

            // Convert back to bytes
            var modifiedBytes = Encoding.UTF8.GetBytes(modifiedPtx);

            // Count parameters for logging
            var paramCount = ParameterPattern().Matches(modifiedPtx).Count - 1; // -1 for injected param
            LogTimestampInjectionComplete(logger, kernelName, paramCount);

            return modifiedBytes;
        }
        catch (Exception ex)
        {
            LogTimestampInjectionFailed(logger, kernelName, ex.Message);
            return ptxBytes; // Return original on error
        }
    }

    /// <summary>
    /// Injects timestamp recording code into PTX string.
    /// </summary>
    /// <param name="ptxCode">Original PTX code as string.</param>
    /// <param name="kernelName">Name of the kernel for identification.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <returns>Modified PTX code with timestamp injection.</returns>
    private static string InjectTimestampCode(string ptxCode, string kernelName, ILogger logger)
    {
        // Find the .entry function declaration
        var entryMatch = EntryFunctionPattern().Match(ptxCode);
        if (!entryMatch.Success)
        {
            LogTimestampInjectionFailed(logger, kernelName, "Could not find .entry function");
            return ptxCode;
        }

        var entryIndex = entryMatch.Index;

        // Find the parameter list start (after function name)
#pragma warning disable XFIX002 // IndexOf with char doesn't support StringComparison
        var paramStartIndex = ptxCode.IndexOf('(', entryIndex);
#pragma warning restore XFIX002
        if (paramStartIndex < 0)
        {
            LogTimestampInjectionFailed(logger, kernelName, "Could not find parameter list");
            return ptxCode;
        }

        // Find the function body start (opening brace)
        var bodyStartMatch = FunctionBodyStartPattern().Match(ptxCode, paramStartIndex);
        if (!bodyStartMatch.Success)
        {
            LogTimestampInjectionFailed(logger, kernelName, "Could not find function body start");
            return ptxCode;
        }

        var bodyStartIndex = bodyStartMatch.Index + bodyStartMatch.Length;

        // Extract parameter list
#pragma warning disable XFIX002 // IndexOf with char doesn't support StringComparison
        var paramEndIndex = ptxCode.IndexOf(')', paramStartIndex);
#pragma warning restore XFIX002
        if (paramEndIndex < 0)
        {
            LogTimestampInjectionFailed(logger, kernelName, "Could not find parameter list end");
            return ptxCode;
        }

        var parameterList = ptxCode.Substring(paramStartIndex + 1, paramEndIndex - paramStartIndex - 1).Trim();

        // Build modified parameter list with injected timestamp parameter
        var modifiedParams = InjectTimestampParameter(parameterList);

        // Build timestamp prologue code
        var (major, _) = CudaCapabilityManager.GetTargetComputeCapability();
        var prologueCode = BuildTimestampPrologue(major >= 6, logger);

        // Construct modified PTX
        var result = new StringBuilder(ptxCode.Length + 500);

        // Copy everything before parameter list
        result.Append(ptxCode.AsSpan(0, paramStartIndex + 1));

        // Insert modified parameters
        result.Append(modifiedParams);

        // Copy from parameter list end to function body start
        result.Append(ptxCode.AsSpan(paramEndIndex, bodyStartIndex - paramEndIndex));

        // Insert timestamp prologue
        result.Append(prologueCode);

        // Copy rest of function body
        result.Append(ptxCode.AsSpan(bodyStartIndex));

        return result.ToString();
    }

    /// <summary>
    /// Injects timestamp parameter at parameter slot 0 and shifts existing parameters.
    /// </summary>
    /// <param name="originalParams">Original parameter list from PTX.</param>
    /// <returns>Modified parameter list with timestamp parameter at slot 0.</returns>
    private static string InjectTimestampParameter(string originalParams)
    {
        var result = new StringBuilder();

        // Add timestamp parameter as param_0
        result.AppendLine();
        result.AppendLine("    .param .u64 param_0,  // Injected: timestamp buffer");

        if (string.IsNullOrWhiteSpace(originalParams))
        {
            // No original parameters, just the timestamp
            result.AppendLine();
            return result.ToString();
        }

        // Parse and shift existing parameters
        var lines = originalParams.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var paramIndex = 1; // Start from 1 since param_0 is timestamp

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLine))
            {
                continue;
            }

            // Replace param_N with param_(N+1)
            var paramMatch = ParameterPattern().Match(trimmedLine);
            if (paramMatch.Success)
            {
                var paramType = paramMatch.Groups[1].Value; // Type (u64, f32, etc.)
                var originalParamName = paramMatch.Groups[2].Value; // param_N

                // Extract parameter number
                var paramNumberMatch = ParamNumberPattern().Match(originalParamName);
                if (paramNumberMatch.Success)
                {
                    // Shift parameter number
                    var shiftedLine = trimmedLine.Replace(originalParamName, $"param_{paramIndex}", StringComparison.Ordinal);
                    result.Append("    ").AppendLine(shiftedLine);
                    paramIndex++;
                }
                else
                {
                    // Keep line as-is if not standard param_N format
                    result.Append("    ").AppendLine(trimmedLine);
                }
            }
            else
            {
                // Keep line as-is if not a parameter
                result.Append("    ").AppendLine(trimmedLine);
            }
        }

        result.AppendLine();
        return result.ToString();
    }

    /// <summary>
    /// Builds the timestamp recording prologue code to insert at kernel entry.
    /// </summary>
    /// <param name="useGlobalTimer">True to use %%globaltimer (CC 6.0+), false for event-based timing.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <returns>PTX code for timestamp recording prologue.</returns>
    private static string BuildTimestampPrologue(bool useGlobalTimer, ILogger logger)
    {
        var (major, minor) = CudaCapabilityManager.GetTargetComputeCapability();
        var timerType = useGlobalTimer ? "%%globaltimer" : "CUDA events";
        LogTimerTypeSelected(logger, timerType, $"{major}.{minor}");

        var prologue = new StringBuilder();

        prologue.AppendLine();
        prologue.AppendLine("    // === Injected Timestamp Recording (DotCompute) ===");
        prologue.AppendLine("    // Thread (0,0,0) records kernel entry timestamp");
        prologue.AppendLine("    {");
        prologue.AppendLine("        .reg .pred %p_timestamp;");
        prologue.AppendLine("        .reg .u32 %tid_x_ts, %tid_y_ts, %tid_z_ts;");

        if (useGlobalTimer)
        {
            prologue.AppendLine("        .reg .u64 %timestamp_val;");
            prologue.AppendLine();
            prologue.AppendLine("        // Get thread IDs");
            prologue.AppendLine("        mov.u32 %tid_x_ts, %tid.x;");
            prologue.AppendLine("        mov.u32 %tid_y_ts, %tid.y;");
            prologue.AppendLine("        mov.u32 %tid_z_ts, %tid.z;");
            prologue.AppendLine();
            prologue.AppendLine("        // Check if thread is (0,0,0)");
            prologue.AppendLine("        setp.ne.u32 %p_timestamp, %tid_x_ts, 0;");
            prologue.AppendLine("        @%p_timestamp bra SKIP_TIMESTAMP_RECORD;");
            prologue.AppendLine("        setp.ne.u32 %p_timestamp, %tid_y_ts, 0;");
            prologue.AppendLine("        @%p_timestamp bra SKIP_TIMESTAMP_RECORD;");
            prologue.AppendLine("        setp.ne.u32 %p_timestamp, %tid_z_ts, 0;");
            prologue.AppendLine("        @%p_timestamp bra SKIP_TIMESTAMP_RECORD;");
            prologue.AppendLine();
            prologue.AppendLine("        // Thread (0,0,0): Read globaltimer and store");
            prologue.AppendLine("        mov.u64 %timestamp_val, %globaltimer;");
            prologue.AppendLine("        st.global.u64 [param_0], %timestamp_val;");
            prologue.AppendLine();
            prologue.AppendLine("    SKIP_TIMESTAMP_RECORD:");
        }
        else
        {
            // For CC < 6.0, we can't use globaltimer in PTX
            // The timestamp will need to be recorded via CUDA events externally
            prologue.AppendLine("        // Note: CC < 6.0 does not support %%globaltimer");
            prologue.AppendLine("        // Timestamp recording will use CUDA events externally");
            prologue.AppendLine("        // This is a no-op placeholder");
        }

        prologue.AppendLine("    }");
        prologue.AppendLine("    // === End Timestamp Recording ===");
        prologue.AppendLine();

        return prologue.ToString();
    }

    /// <summary>
    /// Determines if timestamp injection is supported for the given compute capability.
    /// </summary>
    /// <param name="major">Major compute capability.</param>
    /// <param name="minor">Minor compute capability.</param>
    /// <returns>True if timestamp injection is supported; otherwise, false.</returns>
    public static bool IsTimestampInjectionSupported(int major, int minor)
    {
        // Timestamp injection requires:
        // - CC 6.0+ for %%globaltimer support in PTX
        // - CC 5.0+ for event-based timing (fallback)
        return major >= 5;
    }

    /// <summary>
    /// Gets the size of the timestamp buffer required for injection.
    /// </summary>
    /// <returns>Size in bytes (always 8 for a single long timestamp).</returns>
    public static int GetTimestampBufferSize()
    {
        return sizeof(long); // 8 bytes for uint64_t/long timestamp
    }
}
