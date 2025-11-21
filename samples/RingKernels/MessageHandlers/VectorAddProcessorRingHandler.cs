// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;

namespace DotCompute.Samples.RingKernels.MessageHandlers;

/// <summary>
/// C# message handler for VectorAddProcessorRing ring kernel operations.
/// This handler will be automatically translated to CUDA C using CSharpToCudaTranslator.
/// </summary>
/// <remarks>
/// <para>
/// <b>Handler Naming Convention:</b>
/// - Message type: VectorAddProcessorRingRequest
/// - Handler class: VectorAddProcessorRingHandler
/// - Generated CUDA function: process_vector_add_processor_ring_message
/// </para>
/// <para>
/// <b>Translation Requirements:</b>
/// - Class must be static
/// - Method must be named "ProcessMessage"
/// - Parameters must be (Span&lt;byte&gt;, int, Span&lt;byte&gt;, int)
/// - Return type must be bool
/// - Use only imperative constructs (no LINQ, async/await)
/// </para>
/// </remarks>
public static class VectorAddProcessorRingHandler
{
    /// <summary>
    /// Processes a VectorAddProcessorRing message.
    /// This method will be translated to CUDA C and called from the persistent ring kernel.
    /// </summary>
    /// <param name="inputBuffer">Input message buffer containing serialized VectorAddProcessorRingRequest</param>
    /// <param name="inputSize">Maximum size of input buffer</param>
    /// <param name="outputBuffer">Output message buffer for serialized VectorAddProcessorRingResponse</param>
    /// <param name="outputSize">Maximum size of output buffer</param>
    /// <returns>true if message was processed successfully, false otherwise</returns>
    public static bool ProcessMessage(
        Span<byte> inputBuffer,
        int inputSize,
        Span<byte> outputBuffer,
        int outputSize)
    {
        // Validate minimum buffer sizes
        // VectorAddProcessorRingRequest: MessageId(16) + Priority(1) + CorrelationId(17) + A(4) + B(4) = 42 bytes
        // VectorAddProcessorRingResponse: MessageId(16) + Priority(1) + CorrelationId(17) + Result(4) = 38 bytes
        if (inputSize < 42 || outputSize < 38)
        {
            return false;
        }

        // ============================================================================
        // STEP 1: Deserialize VectorAddProcessorRingRequest from inputBuffer
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

        // Read A (4 bytes, float)
        float a = BitConverter.ToSingle(inputBuffer.Slice(offset, 4));
        offset += 4;

        // Read B (4 bytes, float)
        float b = BitConverter.ToSingle(inputBuffer.Slice(offset, 4));

        // ============================================================================
        // STEP 2: Execute VectorAdd logic
        // ============================================================================

        float result = a + b;

        // ============================================================================
        // STEP 3: Serialize VectorAddProcessorRingResponse to outputBuffer
        // ============================================================================

        offset = 0;

        // Write MessageId (16 bytes) - create new MessageId
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

        // Write Result (4 bytes, float)
        if (!BitConverter.TryWriteBytes(outputBuffer.Slice(offset, 4), result))
        {
            return false;
        }

        return true; // Success
    }
}
