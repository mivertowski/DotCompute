// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;

namespace DotCompute.Samples.RingKernels.MessageHandlers;

/// <summary>
/// Simple test handler for TestSimple ring kernel.
/// </summary>
public static class TestSimpleHandler
{
    /// <summary>
    /// Processes a TestSimple message - minimal echo handler.
    /// </summary>
    public static bool ProcessMessage(
        Span<byte> inputBuffer,
        int inputSize,
        Span<byte> outputBuffer,
        int outputSize)
    {
        // Minimal validation
        if (inputSize < 1 || outputSize < 1)
        {
            return false;
        }

        // Simple echo: copy first byte from input to output
        outputBuffer[0] = inputBuffer[0];

        return true;
    }
}
