// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// VectorAdd Message Processing Implementation
// Auto-generated MemoryPack CUDA integration

#ifndef DOTCOMPUTE_VECTORADD_SERIALIZATION_CU
#define DOTCOMPUTE_VECTORADD_SERIALIZATION_CU

#include "VectorAddMessages.cuh"

/// <summary>
/// Process a VectorAdd message: Deserialize request, compute, serialize response.
/// </summary>
/// <param name="input_buffer">Input buffer containing VectorAddRequest (42 bytes)</param>
/// <param name="input_size">Size of input buffer</param>
/// <param name="output_buffer">Output buffer for VectorAddResponse (38 bytes)</param>
/// <param name="output_size">Size of output buffer</param>
/// <returns>true if processing succeeded, false otherwise</returns>
__device__ bool process_vector_add_message(
    const unsigned char* input_buffer,
    int input_size,
    unsigned char* output_buffer,
    int output_size)
{
    // Validate buffer sizes
    if (input_size < 42)
    {
        return false; // Input buffer too small for VectorAddRequest
    }

    if (output_size < 38)
    {
        return false; // Output buffer too small for VectorAddResponse
    }

    // Deserialize VectorAddRequest
    vector_add_request request;
    if (!deserialize_vector_add_request(input_buffer, input_size, &request))
    {
        return false; // Deserialization failed
    }

    // Compute: result = a + b
    float result = request.a + request.b;

    // Create VectorAddResponse
    vector_add_response response;

    // Copy message metadata from request
    #pragma unroll
    for (int i = 0; i < 16; i++)
    {
        response.message_id[i] = request.message_id[i];
    }
    response.priority = request.priority;
    response.correlation_id = request.correlation_id;

    // Set result
    response.result = result;

    // Serialize VectorAddResponse
    serialize_vector_add_response(&response, output_buffer);

    return true;
}

#endif // DOTCOMPUTE_VECTORADD_SERIALIZATION_CU
