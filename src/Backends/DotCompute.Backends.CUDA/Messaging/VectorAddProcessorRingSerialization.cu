// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// VectorAddProcessorRing message handler for CUDA ring kernels
// Self-contained - all serialization code inline (no #include for NVRTC compatibility)

#ifndef VECTOR_ADD_PROCESSOR_RING_SERIALIZATION_CU
#define VECTOR_ADD_PROCESSOR_RING_SERIALIZATION_CU

// VectorAddRequest binary layout (41 bytes total):
// - MessageId: 16 bytes (GUID)
// - Priority: 1 byte
// - CorrelationId: 16 bytes (GUID, may be zeros if null)
// - A: 4 bytes (float)
// - B: 4 bytes (float)
struct VectorAddRequest_Ring
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
struct VectorAddResponse_Ring
{
    unsigned char message_id[16];
    unsigned char priority;
    unsigned char correlation_id[16];
    float result;
};

// Deserialize VectorAddRequest from buffer (41 bytes)
__device__ bool deserialize_vector_add_request_ring(
    const unsigned char* buffer,
    int buffer_size,
    VectorAddRequest_Ring* request)
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
__device__ int serialize_vector_add_response_ring(
    unsigned char* buffer,
    int buffer_size,
    const VectorAddResponse_Ring* response)
{
    if (buffer_size < 37)
    {
        return 0;
    }

    int offset = 0;

    // MessageId (16 bytes)
    for (int i = 0; i < 16; i++)
    {
        buffer[offset + i] = response->message_id[i];
    }
    offset += 16;

    // Priority (1 byte)
    buffer[offset] = response->priority;
    offset += 1;

    // CorrelationId (16 bytes)
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
__device__ void create_vector_add_response_ring(
    const VectorAddRequest_Ring* request,
    float result,
    VectorAddResponse_Ring* response)
{
    // Generate new MessageId for response
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

// Handler function with the name expected by the ring kernel dispatcher
// Processes VectorAdd messages: Deserialize request -> Compute a + b -> Serialize response
__device__ bool process_vector_add_processor_ring_message(
    unsigned char* input_buffer,
    int input_size,
    unsigned char* output_buffer,
    int output_size)
{
    // Deserialize request
    VectorAddRequest_Ring request;
    if (!deserialize_vector_add_request_ring(input_buffer, input_size, &request))
    {
        return false;
    }

    // Compute VectorAdd: result = a + b
    float result = request.a + request.b;

    // Create response
    VectorAddResponse_Ring response;
    create_vector_add_response_ring(&request, result, &response);

    // Serialize response
    int bytes_written = serialize_vector_add_response_ring(output_buffer, output_size, &response);

    return bytes_written == 37;
}

#endif // VECTOR_ADD_PROCESSOR_RING_SERIALIZATION_CU
