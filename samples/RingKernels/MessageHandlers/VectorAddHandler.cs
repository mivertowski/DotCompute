// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using System.Runtime.InteropServices;

namespace DotCompute.Samples.RingKernels.MessageHandlers;

/// <summary>
/// C# message handler for VectorAdd ring kernel operations.
/// This handler will be automatically translated to CUDA C using CSharpToCudaTranslator.
/// </summary>
/// <remarks>
/// <para>
/// <b>Translation Requirements:</b>
/// - Method must be static
/// - Parameters must use primitive types or Span&lt;byte&gt;
/// - No LINQ, async/await, or complex C# features
/// - Use only imperative constructs (if/else, for, while, etc.)
/// </para>
/// <para>
/// <b>Binary Format Compatibility:</b>
/// - Must match the message structures defined in VectorAddMessages.cs
/// - VectorAddRequest: 42 bytes (MessageId + Priority + CorrelationId + A + B)
/// - VectorAddResponse: 38 bytes (MessageId + Priority + CorrelationId + Result)
/// </para>
/// </remarks>
public static class VectorAddHandler
{
    /// <summary>
    /// Processes a VectorAdd ring kernel message.
    /// This method will be translated to CUDA C and called from the persistent ring kernel.
    /// </summary>
    /// <param name="inputBuffer">Input message buffer containing serialized VectorAddRequest (42 bytes)</param>
    /// <param name="inputSize">Maximum size of input buffer</param>
    /// <param name="outputBuffer">Output message buffer for serialized VectorAddResponse (38 bytes)</param>
    /// <param name="outputSize">Maximum size of output buffer</param>
    /// <returns>true if message was processed successfully, false otherwise</returns>
    public static bool ProcessMessage(
        Span<byte> inputBuffer,
        int inputSize,
        Span<byte> outputBuffer,
        int outputSize)
    {
        // Validate buffer sizes
        if (inputSize < 42 || outputSize < 38)
        {
            return false;
        }

        // ============================================================================
        // STEP 1: Deserialize VectorAddRequest from inputBuffer
        // ============================================================================

        int offset = 0;

        // Read MessageId (16 bytes)
        Span<byte> requestMessageId = inputBuffer.Slice(offset, 16);
        offset += 16;

        // Read Priority (1 byte)
        byte requestPriority = inputBuffer[offset];
        offset += 1;

        // Read CorrelationId (1 byte presence + 16 bytes value)
        byte hasCorrelationId = inputBuffer[offset];
        offset += 1;

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
        // STEP 3: Serialize VectorAddResponse to outputBuffer
        // ============================================================================

        offset = 0;

        // Write MessageId (16 bytes) - create new MessageId by XOR-ing request MessageId
        // In production, use proper GUID generation
        for (int i = 0; i < 16; i++)
        {
            outputBuffer[offset + i] = (byte)(requestMessageId[i] ^ 0xFF);
        }
        offset += 16;

        // Write Priority (1 byte) - preserve from request
        outputBuffer[offset] = requestPriority;
        offset += 1;

        // Write CorrelationId (1 byte presence + 16 bytes value)
        outputBuffer[offset] = hasCorrelationId;
        offset += 1;

        for (int i = 0; i < 16; i++)
        {
            outputBuffer[offset + i] = correlationId[i];
        }
        offset += 16;

        // Write Result (4 bytes, float)
        if (!BitConverter.TryWriteBytes(outputBuffer.Slice(offset, 4), result))
        {
            return false; // Failed to write result
        }

        return true; // Success
    }

    /// <summary>
    /// Alternative implementation using unsafe pointers for better performance.
    /// This demonstrates C# unsafe code that translates well to CUDA.
    /// </summary>
    /// <remarks>
    /// Note: The translator should handle unsafe code and pointer arithmetic.
    /// </remarks>
    public static unsafe bool ProcessMessageUnsafe(
        byte* inputBuffer,
        int inputSize,
        byte* outputBuffer,
        int outputSize)
    {
        // Validate buffer sizes
        if (inputSize < 42 || outputSize < 38)
        {
            return false;
        }

        // Deserialize request using pointer arithmetic
        int offset = 0;

        byte* requestMessageId = inputBuffer + offset;
        offset += 16;

        byte requestPriority = *(inputBuffer + offset);
        offset += 1;

        byte hasCorrelationId = *(inputBuffer + offset);
        offset += 1;

        byte* correlationId = inputBuffer + offset;
        offset += 16;

        float a = *((float*)(inputBuffer + offset));
        offset += 4;

        float b = *((float*)(inputBuffer + offset));

        // Execute logic
        float result = a + b;

        // Serialize response
        offset = 0;

        // Copy MessageId (modified)
        for (int i = 0; i < 16; i++)
        {
            outputBuffer[offset + i] = (byte)(requestMessageId[i] ^ 0xFF);
        }
        offset += 16;

        // Copy Priority
        *(outputBuffer + offset) = requestPriority;
        offset += 1;

        // Copy CorrelationId
        *(outputBuffer + offset) = hasCorrelationId;
        offset += 1;

        for (int i = 0; i < 16; i++)
        {
            outputBuffer[offset + i] = correlationId[i];
        }
        offset += 16;

        // Write Result
        *((float*)(outputBuffer + offset)) = result;

        return true;
    }
}
