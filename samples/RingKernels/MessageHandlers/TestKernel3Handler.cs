// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;

namespace DotCompute.Samples.RingKernels.MessageHandlers;

/// <summary>
/// Minimal test handler for TestKernel3 ring kernel.
/// Echoes the input value to output.
/// </summary>
public static class TestKernel3Handler
{
    public static bool ProcessMessage(
        Span<byte> inputBuffer,
        int inputSize,
        Span<byte> outputBuffer,
        int outputSize)
    {
        if (inputSize < 1 || outputSize < 1) return false;
        outputBuffer[0] = inputBuffer[0]; // Echo first byte
        return true;
    }
}
