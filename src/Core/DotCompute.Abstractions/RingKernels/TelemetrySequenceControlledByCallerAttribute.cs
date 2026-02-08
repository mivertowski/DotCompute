// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.RingKernels;

/// <summary>
/// Indicates that telemetry call sequencing is controlled by the caller,
/// suppressing DC015, DC016, DC017 diagnostics for this method.
/// </summary>
/// <remarks>
/// <para>
/// Apply this attribute to wrapper methods that delegate Ring Kernel operations
/// where the caller is responsible for proper telemetry initialization and sequencing.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// [TelemetrySequenceControlledByCaller(Message = "Telemetry enabled by container initialization")]
/// public async Task&lt;RingKernelTelemetry&gt; GetMetricsAsync()
/// {
///     // DC015 would normally fire here, but is suppressed by the attribute
///     return await _runtime.GetTelemetryAsync();
/// }
/// </code>
/// </para>
/// <para>
/// This attribute can be applied to:
/// <list type="bullet">
///   <item><description>Methods that wrap Ring Kernel telemetry operations</description></item>
///   <item><description>Classes where all methods delegate to caller-controlled sequences</description></item>
/// </list>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class TelemetrySequenceControlledByCallerAttribute : Attribute
{
    /// <summary>
    /// Gets or sets an optional message explaining who controls the telemetry sequence.
    /// </summary>
    /// <value>
    /// A descriptive message for documentation purposes, such as
    /// "Telemetry enabled by container initialization" or "Caller ensures proper sequencing".
    /// </value>
    public string? Message { get; set; }
}
