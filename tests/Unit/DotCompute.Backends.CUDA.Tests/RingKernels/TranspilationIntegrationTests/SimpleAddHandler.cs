// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;

namespace DotCompute.Backends.CUDA.Tests.RingKernels.TranspilationIntegrationTests;

/// <summary>
/// C# message handler for SimpleAdd ring kernel.
/// This handler will be automatically translated to CUDA C using CSharpToCudaTranslator.
/// </summary>
/// <remarks>
/// <para>
/// <b>Handler Convention:</b>
/// - Class name: {BaseName}Handler → SimpleAddHandler (matches SimpleAddRequest/SimpleAddResponse)
/// - Generated CUDA function: process_simple_add_message
/// </para>
/// <para>
/// <b>Translation Requirements:</b>
/// - Class must be static
/// - Method must be named "ProcessMessage"
/// - Parameters: (Span&lt;byte&gt;, int, Span&lt;byte&gt;, int)
/// - Return type: bool
/// </para>
/// </remarks>
public static class SimpleAddHandler
{
    /// <summary>
    /// Processes a SimpleAdd ring kernel message.
    /// This method will be translated to CUDA C: process_simple_add_message()
    /// </summary>
    /// <param name="inputBuffer">Input message buffer containing serialized SimpleAddRequest</param>
    /// <param name="inputSize">Maximum size of input buffer</param>
    /// <param name="outputBuffer">Output message buffer for serialized SimpleAddResponse</param>
    /// <param name="outputSize">Maximum size of output buffer</param>
    /// <returns>true if message was processed successfully, false otherwise</returns>
    public static bool ProcessMessage(
        Span<byte> inputBuffer,
        int inputSize,
        Span<byte> outputBuffer,
        int outputSize)
    {
        // Validate minimum buffer sizes
        // SimpleAddRequest: MessageId(16) + Priority(1) + CorrelationId(17) + A(4) + B(4) = 42 bytes
        // SimpleAddResponse: MessageId(16) + Priority(1) + CorrelationId(17) + Result(4) = 38 bytes
        if (inputSize < 42 || outputSize < 38)
        {
            return false;
        }

        // ============================================================================
        // STEP 1: Deserialize SimpleAddRequest from inputBuffer
        // ============================================================================

        int offset = 0;

        // Read MessageId (16 bytes)
        Span<byte> requestMessageId = inputBuffer.Slice(offset, 16);
        offset += 16;

        // Read Priority (1 byte)
        byte requestPriority = inputBuffer[offset];
        offset += 1;

        // Read CorrelationId presence flag (1 byte)
        byte hasCorrelationId = inputBuffer[offset];
        offset += 1;

        // Read CorrelationId value (16 bytes)
        Span<byte> correlationId = inputBuffer.Slice(offset, 16);
        offset += 16;

        // Read A (4 bytes, int32)
        int a = BitConverter.ToInt32(inputBuffer.Slice(offset, 4));
        offset += 4;

        // Read B (4 bytes, int32)
        int b = BitConverter.ToInt32(inputBuffer.Slice(offset, 4));

        // ============================================================================
        // STEP 2: Execute SimpleAdd logic
        // ============================================================================

        int result = a + b;

        // ============================================================================
        // STEP 3: Serialize SimpleAddResponse to outputBuffer
        // ============================================================================

        offset = 0;

        // Write new MessageId (16 bytes) - derived from request
        for (int i = 0; i < 16; i++)
        {
            outputBuffer[offset + i] = (byte)(requestMessageId[i] ^ 0xFF);
        }
        offset += 16;

        // Write Priority (1 byte) - preserve from request
        outputBuffer[offset] = requestPriority;
        offset += 1;

        // Write CorrelationId presence (1 byte)
        outputBuffer[offset] = hasCorrelationId;
        offset += 1;

        // Write CorrelationId (16 bytes)
        for (int i = 0; i < 16; i++)
        {
            outputBuffer[offset + i] = correlationId[i];
        }
        offset += 16;

        // Write Result (4 bytes, int32)
        if (!BitConverter.TryWriteBytes(outputBuffer.Slice(offset, 4), result))
        {
            return false;
        }

        return true; // Success
    }
}
