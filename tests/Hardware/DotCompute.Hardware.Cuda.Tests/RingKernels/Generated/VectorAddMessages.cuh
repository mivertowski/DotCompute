// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// Auto-generated MemoryPack CUDA serialization code
// Type: DotCompute.Hardware.Cuda.Tests.Messaging.VectorAddRequest
// Total Size: 42 bytes
// Fixed Size: Yes

#ifndef DOTCOMPUTE_VECTORADD_MESSAGES_CUH
#define DOTCOMPUTE_VECTORADD_MESSAGES_CUH

#include <cstdint>

// VectorAddRequest message structure
struct vector_add_request
{
    unsigned char message_id[16];
    uint8_t priority;
    struct { bool has_value; unsigned char value[16]; } correlation_id;
    float a;
    float b;
};

__device__ bool deserialize_vector_add_request(
    const unsigned char* buffer,
    int buffer_size,
    vector_add_request* out)
{
    // Bounds check: message must be at least 42 bytes
    if (buffer_size < 42)
    {
        return false; // Buffer too small
    }

    // Deserialize fields in MemoryPack order
    // MessageId: System.Guid at offset 0 (16 bytes)
    #pragma unroll
    for (int i = 0; i < 16; i++)
    {
        out->message_id[i] = buffer[0 + i];
    }

    // Priority: System.Byte at offset 16 (1 bytes)
    out->priority = buffer[16];

    // CorrelationId: System.Guid at offset 17 (17 bytes)
    out->correlation_id.has_value = buffer[17] != 0;
    if (out->correlation_id.has_value)
    {
        #pragma unroll
        for (int i = 0; i < 16; i++)
        {
            out->correlation_id.value[i] = buffer[18 + i];
        }
    }

    // A: System.Single at offset 34 (4 bytes)
    out->a = *reinterpret_cast<const float*>(&buffer[34]);

    // B: System.Single at offset 38 (4 bytes)
    out->b = *reinterpret_cast<const float*>(&buffer[38]);

    return true; // Deserialization successful
}

__device__ void serialize_vector_add_request(
    const vector_add_request* input,
    unsigned char* buffer)
{
    // Serialize fields in MemoryPack order
    // MessageId: System.Guid at offset 0
    #pragma unroll
    for (int i = 0; i < 16; i++)
    {
        buffer[0 + i] = input->message_id[i];
    }

    // Priority: System.Byte at offset 16
    buffer[16] = input->priority;

    // CorrelationId: System.Guid at offset 17
    buffer[17] = input->correlation_id.has_value ? 1 : 0;
    if (input->correlation_id.has_value)
    {
        #pragma unroll
        for (int i = 0; i < 16; i++)
        {
            buffer[18 + i] = input->correlation_id.value[i];
        }
    }

    // A: System.Single at offset 34
    *reinterpret_cast<float*>(&buffer[34]) = input->a;

    // B: System.Single at offset 38
    *reinterpret_cast<float*>(&buffer[38]) = input->b;

}

// Auto-generated MemoryPack CUDA serialization code
// Type: DotCompute.Hardware.Cuda.Tests.Messaging.VectorAddResponse
// Total Size: 38 bytes
// Fixed Size: Yes

// VectorAddResponse message structure
struct vector_add_response
{
    unsigned char message_id[16];
    uint8_t priority;
    struct { bool has_value; unsigned char value[16]; } correlation_id;
    float result;
};

__device__ bool deserialize_vector_add_response(
    const unsigned char* buffer,
    int buffer_size,
    vector_add_response* out)
{
    // Bounds check: message must be at least 38 bytes
    if (buffer_size < 38)
    {
        return false; // Buffer too small
    }

    // Deserialize fields in MemoryPack order
    // MessageId: System.Guid at offset 0 (16 bytes)
    #pragma unroll
    for (int i = 0; i < 16; i++)
    {
        out->message_id[i] = buffer[0 + i];
    }

    // Priority: System.Byte at offset 16 (1 bytes)
    out->priority = buffer[16];

    // CorrelationId: System.Guid at offset 17 (17 bytes)
    out->correlation_id.has_value = buffer[17] != 0;
    if (out->correlation_id.has_value)
    {
        #pragma unroll
        for (int i = 0; i < 16; i++)
        {
            out->correlation_id.value[i] = buffer[18 + i];
        }
    }

    // Result: System.Single at offset 34 (4 bytes)
    out->result = *reinterpret_cast<const float*>(&buffer[34]);

    return true; // Deserialization successful
}

__device__ void serialize_vector_add_response(
    const vector_add_response* input,
    unsigned char* buffer)
{
    // Serialize fields in MemoryPack order
    // MessageId: System.Guid at offset 0
    #pragma unroll
    for (int i = 0; i < 16; i++)
    {
        buffer[0 + i] = input->message_id[i];
    }

    // Priority: System.Byte at offset 16
    buffer[16] = input->priority;

    // CorrelationId: System.Guid at offset 17
    buffer[17] = input->correlation_id.has_value ? 1 : 0;
    if (input->correlation_id.has_value)
    {
        #pragma unroll
        for (int i = 0; i < 16; i++)
        {
            buffer[18 + i] = input->correlation_id.value[i];
        }
    }

    // Result: System.Single at offset 34
    *reinterpret_cast<float*>(&buffer[34]) = input->result;

}

#endif // DOTCOMPUTE_VECTORADD_MESSAGES_CUH
