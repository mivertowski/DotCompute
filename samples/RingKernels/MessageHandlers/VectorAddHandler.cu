// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// Reference CUDA implementation of VectorAdd ring kernel message handler
// This serves as the "golden master" for C# to CUDA translation validation

#include <cstdint>

/// <summary>
/// Processes a VectorAdd ring kernel message.
/// This is the reference implementation that demonstrates the expected CUDA output
/// from translating the C# handler method.
/// </summary>
/// <param name="input_buffer">Input message buffer containing serialized VectorAddRequest</param>
/// <param name="input_size">Maximum size of input buffer (must be >= 42 bytes)</param>
/// <param name="output_buffer">Output message buffer for serialized VectorAddResponse</param>
/// <param name="output_size">Maximum size of output buffer (must be >= 38 bytes)</param>
/// <returns>true if message was processed successfully, false otherwise</returns>
/// <remarks>
/// Binary Format (VectorAddRequest - 42 bytes):
///   - MessageId: 16 bytes (Guid, little-endian)
///   - Priority: 1 byte (uint8_t)
///   - CorrelationId: 1 byte (presence flag) + 16 bytes (Guid, nullable)
///   - A: 4 bytes (float, IEEE 754 single precision)
///   - B: 4 bytes (float, IEEE 754 single precision)
///
/// Binary Format (VectorAddResponse - 38 bytes):
///   - MessageId: 16 bytes (Guid, little-endian)
///   - Priority: 1 byte (uint8_t)
///   - CorrelationId: 1 byte (presence flag) + 16 bytes (Guid, nullable)
///   - Result: 4 bytes (float, IEEE 754 single precision)
/// </remarks>
__device__ bool process_vector_add_message(
    unsigned char* input_buffer,
    int32_t input_size,
    unsigned char* output_buffer,
    int32_t output_size)
{
    // Validate buffer sizes
    if (input_size < 42 || output_size < 38) {
        return false; // Insufficient buffer size
    }

    // ============================================================================
    // STEP 1: Deserialize VectorAddRequest from input_buffer
    // ============================================================================

    int32_t offset = 0;

    // Read MessageId (16 bytes)
    // Note: Guid is stored as 16 bytes in little-endian format
    unsigned char request_message_id[16];
    for (int32_t i = 0; i < 16; i++) {
        request_message_id[i] = input_buffer[offset + i];
    }
    offset += 16;

    // Read Priority (1 byte)
    uint8_t request_priority = input_buffer[offset];
    offset += 1;

    // Read CorrelationId (1 byte presence + 16 bytes value)
    uint8_t has_correlation_id = input_buffer[offset];
    offset += 1;

    unsigned char correlation_id[16];
    for (int32_t i = 0; i < 16; i++) {
        correlation_id[i] = input_buffer[offset + i];
    }
    offset += 16;

    // Read A (4 bytes, float)
    float a;
    unsigned char* a_bytes = (unsigned char*)&a;
    for (int32_t i = 0; i < 4; i++) {
        a_bytes[i] = input_buffer[offset + i];
    }
    offset += 4;

    // Read B (4 bytes, float)
    float b;
    unsigned char* b_bytes = (unsigned char*)&b;
    for (int32_t i = 0; i < 4; i++) {
        b_bytes[i] = input_buffer[offset + i];
    }
    // offset += 4; // Final offset: 42 bytes

    // ============================================================================
    // STEP 2: Execute VectorAdd logic
    // ============================================================================

    float result = a + b;

    // ============================================================================
    // STEP 3: Serialize VectorAddResponse to output_buffer
    // ============================================================================

    offset = 0;

    // Write MessageId (16 bytes) - new Guid for response
    // For simplicity, we'll create a new MessageId by XOR-ing request MessageId
    // In production, use proper GUID generation
    for (int32_t i = 0; i < 16; i++) {
        output_buffer[offset + i] = request_message_id[i] ^ 0xFF;
    }
    offset += 16;

    // Write Priority (1 byte) - preserve from request
    output_buffer[offset] = request_priority;
    offset += 1;

    // Write CorrelationId (1 byte presence + 16 bytes value)
    output_buffer[offset] = has_correlation_id;
    offset += 1;
    for (int32_t i = 0; i < 16; i++) {
        output_buffer[offset + i] = correlation_id[i];
    }
    offset += 16;

    // Write Result (4 bytes, float)
    unsigned char* result_bytes = (unsigned char*)&result;
    for (int32_t i = 0; i < 4; i++) {
        output_buffer[offset + i] = result_bytes[i];
    }
    // offset += 4; // Final offset: 38 bytes

    return true; // Success
}

// ============================================================================
// Alternative Implementation Using Pointers (More Efficient)
// ============================================================================

/// <summary>
/// Optimized version using direct pointer casting for better performance.
/// This is what a proficient CUDA developer would write.
/// </summary>
__device__ bool process_vector_add_message_optimized(
    unsigned char* input_buffer,
    int32_t input_size,
    unsigned char* output_buffer,
    int32_t output_size)
{
    // Validate buffer sizes
    if (input_size < 42 || output_size < 38) {
        return false;
    }

    // Deserialize request using pointer arithmetic
    int32_t offset = 0;

    unsigned char* request_message_id = input_buffer + offset;
    offset += 16;

    uint8_t request_priority = *(input_buffer + offset);
    offset += 1;

    uint8_t has_correlation_id = *(input_buffer + offset);
    offset += 1;

    unsigned char* correlation_id = input_buffer + offset;
    offset += 16;

    float a = *((float*)(input_buffer + offset));
    offset += 4;

    float b = *((float*)(input_buffer + offset));

    // Execute logic
    float result = a + b;

    // Serialize response
    offset = 0;

    // Copy MessageId (modified)
    for (int32_t i = 0; i < 16; i++) {
        output_buffer[offset + i] = request_message_id[i] ^ 0xFF;
    }
    offset += 16;

    // Copy Priority
    *(output_buffer + offset) = request_priority;
    offset += 1;

    // Copy CorrelationId
    *(output_buffer + offset) = has_correlation_id;
    offset += 1;

    for (int32_t i = 0; i < 16; i++) {
        output_buffer[offset + i] = correlation_id[i];
    }
    offset += 16;

    // Write Result
    *((float*)(output_buffer + offset)) = result;

    return true;
}
