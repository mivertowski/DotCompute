// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CPU.Kernels.Simd;
using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.SIMD;

/// <summary>
/// LoggerMessage delegates for SimdInstructionSelector.
/// </summary>
public sealed partial class SimdInstructionSelector
{
    // Event ID Range: 7700-7719

    [LoggerMessage(
        EventId = 7700,
        Level = LogLevel.Debug,
        Message = "Selected SIMD strategy {Strategy} for workload: {ElementCount} elements, {DataSize} bytes")]
    private static partial void LogStrategySelected(ILogger logger, SimdExecutionStrategy strategy, long elementCount, long dataSize);

    [LoggerMessage(
        EventId = 7701,
        Level = LogLevel.Debug,
        Message = "SIMD Instruction Selector disposed")]
    private static partial void LogDisposed(ILogger logger);
}
