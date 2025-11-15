// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// VectorAdd message serialization for CUDA ring kernels
// Mirrors the C# VectorAddMessages.cs binary format exactly

#ifndef VECTOR_ADD_SERIALIZATION_CU
#define VECTOR_ADD_SERIALIZATION_CU

// VectorAddRequest binary layout (41 bytes total):
// - MessageId: 16 bytes (GUID)
// - Priority: 1 byte
// - CorrelationId: 16 bytes (GUID, may be zeros if null)
// - A: 4 bytes (float)
// - B: 4 bytes (float)
struct VectorAddRequest
{
    unsigned char message_id[16];
    unsigned char priority;
    unsigned char correlation_id[16];
    float a;
    float b;
};

// VectorAddResponse binary layout (37 bytes total):
// - MessageId: 16 bytes (GUID)
// - Priority: 1 byte
// - CorrelationId: 16 bytes (GUID, should match request's MessageId)
// - Result: 4 bytes (float)
struct VectorAddResponse
{
    unsigned char message_id[16];
    unsigned char priority;
    unsigned char correlation_id[16];
    float result;
};

// Deserialize VectorAddRequest from buffer (41 bytes)
// Returns true if deserialization succeeded, false if buffer too small
__device__ bool deserialize_vector_add_request(
    const unsigned char* buffer,
    int buffer_size,
    VectorAddRequest* request)
{
    if (buffer_size < 41)
    {
        return false;
    }

    int offset = 0;

    // MessageId (16 bytes)
    for (int i = 0; i < 16; i++)
    {
        request->message_id[i] = buffer[offset + i];
    }
    offset += 16;

    // Priority (1 byte)
    request->priority = buffer[offset];
    offset += 1;

    // CorrelationId (16 bytes)
    for (int i = 0; i < 16; i++)
    {
        request->correlation_id[i] = buffer[offset + i];
    }
    offset += 16;

    // A (4 bytes, little-endian float)
    unsigned char a_bytes[4];
    for (int i = 0; i < 4; i++)
    {
        a_bytes[i] = buffer[offset + i];
    }
    request->a = *reinterpret_cast<float*>(a_bytes);
    offset += 4;

    // B (4 bytes, little-endian float)
    unsigned char b_bytes[4];
    for (int i = 0; i < 4; i++)
    {
        b_bytes[i] = buffer[offset + i];
    }
    request->b = *reinterpret_cast<float*>(b_bytes);

    return true;
}

// Serialize VectorAddResponse to buffer (37 bytes)
// Returns number of bytes written (37) or 0 if buffer too small
__device__ int serialize_vector_add_response(
    unsigned char* buffer,
    int buffer_size,
    const VectorAddResponse* response)
{
    if (buffer_size < 37)
    {
        return 0;
    }

    int offset = 0;

    // MessageId (16 bytes) - generate new GUID (simplified: use thread/block IDs)
    // In production, this would use a proper GUID generator
    for (int i = 0; i < 16; i++)
    {
        buffer[offset + i] = response->message_id[i];
    }
    offset += 16;

    // Priority (1 byte)
    buffer[offset] = response->priority;
    offset += 1;

    // CorrelationId (16 bytes) - copy from request's MessageId
    for (int i = 0; i < 16; i++)
    {
        buffer[offset + i] = response->correlation_id[i];
    }
    offset += 16;

    // Result (4 bytes, little-endian float)
    const unsigned char* result_bytes = reinterpret_cast<const unsigned char*>(&response->result);
    for (int i = 0; i < 4; i++)
    {
        buffer[offset + i] = result_bytes[i];
    }
    offset += 4;

    return offset; // Should be 37
}

// Helper: Create response from request with computed result
__device__ void create_vector_add_response(
    const VectorAddRequest* request,
    float result,
    VectorAddResponse* response)
{
    // Generate new MessageId for response (simplified: copy from request and invert first byte)
    for (int i = 0; i < 16; i++)
    {
        response->message_id[i] = request->message_id[i];
    }
    response->message_id[0] = ~request->message_id[0]; // Simple differentiation

    // Copy priority
    response->priority = request->priority;

    // CorrelationId = request's MessageId (for request-response pairing)
    for (int i = 0; i < 16; i++)
    {
        response->correlation_id[i] = request->message_id[i];
    }

    // Set result
    response->result = result;
}

// Complete VectorAdd kernel logic: Deserialize request → Compute → Serialize response
// Returns true if processing succeeded, false on error
__device__ bool process_vector_add_message(
    const unsigned char* input_buffer,
    int input_size,
    unsigned char* output_buffer,
    int output_size)
{
    // Deserialize request
    VectorAddRequest request;
    if (!deserialize_vector_add_request(input_buffer, input_size, &request))
    {
        return false;
    }

    // Compute VectorAdd: result = a + b
    float result = request.a + request.b;

    // Create response
    VectorAddResponse response;
    create_vector_add_response(&request, result, &response);

    // Serialize response
    int bytes_written = serialize_vector_add_response(output_buffer, output_size, &response);

    return bytes_written == 37;
}

#endif // VECTOR_ADD_SERIALIZATION_CU
