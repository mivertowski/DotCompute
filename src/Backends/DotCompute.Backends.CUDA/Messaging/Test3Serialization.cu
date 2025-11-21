// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

// Manual CUDA handler for TestKernel3 ring kernel
// Simple echo implementation for testing

__device__ bool process_test3_message(
    unsigned char* input_buffer,
    int input_size,
    unsigned char* output_buffer,
    int output_size)
{
    if (input_size < 1 || output_size < 1) return false;

    // Echo first byte
    output_buffer[0] = input_buffer[0];
    return true;
}
