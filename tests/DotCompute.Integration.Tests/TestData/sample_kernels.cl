// Sample OpenCL kernels for integration testing

// Basic vector operations
kernel void vector_add(global float* a, global float* b, global float* result, int size) {
    int i = get_global_id(0);
    if (i < size) {
        result[i] = a[i] + b[i];
    }
}

kernel void vector_multiply(global float* a, global float* b, global float* result, int size) {
    int i = get_global_id(0);
    if (i < size) {
        result[i] = a[i] * b[i];
    }
}

kernel void vector_scale(global float* input, global float* output, float scale, int size) {
    int i = get_global_id(0);
    if (i < size) {
        output[i] = input[i] * scale;
    }
}

// Matrix operations
kernel void matrix_multiply(global float* A, global float* B, global float* C, int M, int N, int K) {
    int row = get_global_id(0);
    int col = get_global_id(1);
    
    if (row < M && col < N) {
        float sum = 0.0f;
        for (int k = 0; k < K; k++) {
            sum += A[row * K + k] * B[k * N + col];
        }
        C[row * N + col] = sum;
    }
}

kernel void matrix_transpose(global float* input, global float* output, int rows, int cols) {
    int row = get_global_id(0);
    int col = get_global_id(1);
    
    if (row < rows && col < cols) {
        output[col * rows + row] = input[row * cols + col];
    }
}

// Image processing kernels
kernel void rgb_to_grayscale(global float* input, global float* output, int pixels) {
    int i = get_global_id(0);
    if (i < pixels) {
        int inputIdx = i * 3;
        float r = input[inputIdx];
        float g = input[inputIdx + 1];
        float b = input[inputIdx + 2];
        output[i] = 0.299f * r + 0.587f * g + 0.114f * b;
    }
}

kernel void gaussian_blur_3x3(global float* input, global float* output, int width, int height) {
    int x = get_global_id(0);
    int y = get_global_id(1);
    
    if (x > 0 && x < width - 1 && y > 0 && y < height - 1) {
        float kernel[3][3] = {{1, 2, 1}, {2, 4, 2}, {1, 2, 1}};
        float sum = 0.0f;
        
        for (int dy = -1; dy <= 1; dy++) {
            for (int dx = -1; dx <= 1; dx++) {
                sum += input[(y + dy) * width + (x + dx)] * kernel[dy + 1][dx + 1];
            }
        }
        
        output[y * width + x] = sum / 16.0f;
    }
}

// Mathematical operations
kernel void polynomial_eval(global float* x, global float* coeffs, global float* result, int size, int degree) {
    int i = get_global_id(0);
    if (i < size) {
        float val = 0.0f;
        float x_power = 1.0f;
        
        for (int d = 0; d <= degree; d++) {
            val += coeffs[d] * x_power;
            x_power *= x[i];
        }
        
        result[i] = val;
    }
}

kernel void fft_radix2_step(global float* real, global float* imag, int size, int step) {
    int i = get_global_id(0);
    int j = i + step;
    
    if (j < size) {
        float angle = -2.0f * M_PI * (i % step) / (2 * step);
        float cos_val = cos(angle);
        float sin_val = sin(angle);
        
        float temp_real = real[j] * cos_val - imag[j] * sin_val;
        float temp_imag = real[j] * sin_val + imag[j] * cos_val;
        
        real[j] = real[i] - temp_real;
        imag[j] = imag[i] - temp_imag;
        real[i] = real[i] + temp_real;
        imag[i] = imag[i] + temp_imag;
    }
}

// Reduction operations
kernel void parallel_sum(global float* input, global float* output, local float* scratch, int size) {
    int global_id = get_global_id(0);
    int local_id = get_local_id(0);
    int group_size = get_local_size(0);
    
    // Load data into local memory
    scratch[local_id] = (global_id < size) ? input[global_id] : 0.0f;
    barrier(CLK_LOCAL_MEM_FENCE);
    
    // Perform reduction
    for (int offset = group_size / 2; offset > 0; offset /= 2) {
        if (local_id < offset) {
            scratch[local_id] += scratch[local_id + offset];
        }
        barrier(CLK_LOCAL_MEM_FENCE);
    }
    
    // Write result
    if (local_id == 0) {
        output[get_group_id(0)] = scratch[0];
    }
}

// Statistics kernels
kernel void compute_mean_variance(global float* input, global float* output, int size) {
    // Simple implementation - output[0] = mean, output[1] = variance
    int i = get_global_id(0);
    
    if (i == 0) {
        float sum = 0.0f;
        float sum_sq = 0.0f;
        
        for (int j = 0; j < size; j++) {
            sum += input[j];
            sum_sq += input[j] * input[j];
        }
        
        float mean = sum / size;
        float variance = (sum_sq / size) - (mean * mean);
        
        output[0] = mean;
        output[1] = variance;
    }
}

// Neural network operations
kernel void relu_activation(global float* input, global float* output, int size) {
    int i = get_global_id(0);
    if (i < size) {
        output[i] = fmax(0.0f, input[i]);
    }
}

kernel void sigmoid_activation(global float* input, global float* output, int size) {
    int i = get_global_id(0);
    if (i < size) {
        output[i] = 1.0f / (1.0f + exp(-input[i]));
    }
}

kernel void batch_normalization(global float* input, global float* output, 
                               global float* mean, global float* variance,
                               float epsilon, int size) {
    int i = get_global_id(0);
    if (i < size) {
        output[i] = (input[i] - mean[0]) / sqrt(variance[0] + epsilon);
    }
}