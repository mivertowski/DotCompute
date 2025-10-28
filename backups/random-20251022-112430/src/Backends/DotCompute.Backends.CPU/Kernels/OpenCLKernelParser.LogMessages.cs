// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using Microsoft.Extensions.Logging;

namespace DotCompute.Backends.CPU.Kernels;

/// <summary>
/// LoggerMessage delegates for OpenCLKernelParser.
/// </summary>
internal partial class OpenCLKernelParser
{
    // Event ID Range: 7720-7739

    [LoggerMessage(
        EventId = 7720,
        Level = LogLevel.Debug,
        Message = "Parsing kernel: {EntryPoint}")]
    private static partial void LogParsingKernel(ILogger logger, string entryPoint);
}
