// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

#include <cuda_runtime.h>
#include <cooperative_groups.h>

namespace cg = cooperative_groups;

// Constants for wave equation
#define WAVE_SPEED 1.0f
#define DAMPING 0.999f

// Persistent kernel for 1D acoustic wave propagation
// Uses finite difference method: u(t+1) = 2*u(t) - u(t-1) + c^2 * dt^2/dx^2 * (u[i+1] - 2*u[i] + u[i-1])
extern "C" __global__ void acoustic_wave_1d_persistent(
    float* __restrict__ u_current,    // Current time step
    float* __restrict__ u_previous,   // Previous time step  
    float* __restrict__ u_two_ago,    // Two steps ago
    int* __restrict__ control,        // Control buffer [running, iteration, error, reserved]
    const int nx,                     // Grid width
    const float dx,                   // Spatial step
    const float dt,                   // Time step
    const int max_iterations)
{
    const int tid = blockIdx.x * blockDim.x + threadIdx.x;
    const int stride = blockDim.x * gridDim.x;
    
    const float c2_dt2_dx2 = (WAVE_SPEED * WAVE_SPEED * dt * dt) / (dx * dx);
    
    // Grid-stride loop for persistent execution
    while (control[0] == 1 && control[1] < max_iterations) {
        // Process all points using grid-stride loop
        for (int i = tid; i < nx; i += stride) {
            // Skip boundary points (Dirichlet boundary conditions)
            if (i == 0 || i == nx - 1) {
                u_current[i] = 0.0f;
                continue;
            }
            
            // Finite difference wave equation
            float laplacian = u_previous[i + 1] - 2.0f * u_previous[i] + u_previous[i - 1];
            u_current[i] = 2.0f * u_previous[i] - u_two_ago[i] + c2_dt2_dx2 * laplacian;
            u_current[i] *= DAMPING; // Apply damping
        }
        
        // Synchronize all threads before swapping buffers
        __syncthreads();
        
        // Only thread 0 updates control and swaps pointers
        if (tid == 0) {
            // Rotate buffers: two_ago <- previous <- current <- two_ago
            float* temp = u_two_ago;
            u_two_ago = u_previous;
            u_previous = u_current;
            u_current = temp;
            
            // Update iteration counter
            atomicAdd(&control[1], 1);
        }
        
        __syncthreads();
    }
}

// Persistent kernel for 2D acoustic wave propagation
extern "C" __global__ void acoustic_wave_2d_persistent(
    float* __restrict__ u_current,
    float* __restrict__ u_previous,
    float* __restrict__ u_two_ago,
    int* __restrict__ control,
    const int nx,
    const int ny,
    const float dx,
    const float dy,
    const float dt,
    const int max_iterations)
{
    const int tid = blockIdx.x * blockDim.x + threadIdx.x;
    const int stride = blockDim.x * gridDim.x;
    const int total_points = nx * ny;
    
    const float c2_dt2_dx2 = (WAVE_SPEED * WAVE_SPEED * dt * dt) / (dx * dx);
    const float c2_dt2_dy2 = (WAVE_SPEED * WAVE_SPEED * dt * dt) / (dy * dy);
    
    while (control[0] == 1 && control[1] < max_iterations) {
        for (int idx = tid; idx < total_points; idx += stride) {
            const int i = idx % nx;
            const int j = idx / nx;
            
            // Skip boundary points
            if (i == 0 || i == nx - 1 || j == 0 || j == ny - 1) {
                u_current[idx] = 0.0f;
                continue;
            }
            
            // 2D Laplacian
            float laplacian_x = u_previous[idx + 1] - 2.0f * u_previous[idx] + u_previous[idx - 1];
            float laplacian_y = u_previous[idx + nx] - 2.0f * u_previous[idx] + u_previous[idx - nx];
            
            u_current[idx] = 2.0f * u_previous[idx] - u_two_ago[idx] 
                           + c2_dt2_dx2 * laplacian_x 
                           + c2_dt2_dy2 * laplacian_y;
            u_current[idx] *= DAMPING;
        }
        
        __syncthreads();
        
        if (tid == 0) {
            float* temp = u_two_ago;
            u_two_ago = u_previous;
            u_previous = u_current;
            u_current = temp;
            atomicAdd(&control[1], 1);
        }
        
        __syncthreads();
    }
}

// Persistent kernel for 3D acoustic wave propagation
extern "C" __global__ void acoustic_wave_3d_persistent(
    float* __restrict__ u_current,
    float* __restrict__ u_previous,
    float* __restrict__ u_two_ago,
    int* __restrict__ control,
    const int nx,
    const int ny,
    const int nz,
    const float dx,
    const float dy,
    const float dz,
    const float dt,
    const int max_iterations)
{
    const int tid = blockIdx.x * blockDim.x + threadIdx.x;
    const int stride = blockDim.x * gridDim.x;
    const int total_points = nx * ny * nz;
    
    const float c2_dt2_dx2 = (WAVE_SPEED * WAVE_SPEED * dt * dt) / (dx * dx);
    const float c2_dt2_dy2 = (WAVE_SPEED * WAVE_SPEED * dt * dt) / (dy * dy);
    const float c2_dt2_dz2 = (WAVE_SPEED * WAVE_SPEED * dt * dt) / (dz * dz);
    
    while (control[0] == 1 && control[1] < max_iterations) {
        for (int idx = tid; idx < total_points; idx += stride) {
            const int i = idx % nx;
            const int j = (idx / nx) % ny;
            const int k = idx / (nx * ny);
            
            // Skip boundary points
            if (i == 0 || i == nx - 1 || 
                j == 0 || j == ny - 1 || 
                k == 0 || k == nz - 1) {
                u_current[idx] = 0.0f;
                continue;
            }
            
            // 3D Laplacian
            float laplacian_x = u_previous[idx + 1] - 2.0f * u_previous[idx] + u_previous[idx - 1];
            float laplacian_y = u_previous[idx + nx] - 2.0f * u_previous[idx] + u_previous[idx - nx];
            float laplacian_z = u_previous[idx + nx*ny] - 2.0f * u_previous[idx] + u_previous[idx - nx*ny];
            
            u_current[idx] = 2.0f * u_previous[idx] - u_two_ago[idx]
                           + c2_dt2_dx2 * laplacian_x
                           + c2_dt2_dy2 * laplacian_y
                           + c2_dt2_dz2 * laplacian_z;
            u_current[idx] *= DAMPING;
        }
        
        __syncthreads();
        
        if (tid == 0) {
            float* temp = u_two_ago;
            u_two_ago = u_previous;
            u_previous = u_current;
            u_current = temp;
            atomicAdd(&control[1], 1);
        }
        
        __syncthreads();
    }
}

// Cooperative groups version for grid-wide synchronization
extern "C" __global__ void acoustic_wave_2d_cooperative(
    float* __restrict__ u_current,
    float* __restrict__ u_previous,
    float* __restrict__ u_two_ago,
    int* __restrict__ control,
    const int nx,
    const int ny,
    const float dx,
    const float dy,
    const float dt,
    const int max_iterations)
{
    cg::grid_group grid = cg::this_grid();
    const int tid = grid.thread_rank();
    const int stride = grid.size();
    const int total_points = nx * ny;
    
    const float c2_dt2_dx2 = (WAVE_SPEED * WAVE_SPEED * dt * dt) / (dx * dx);
    const float c2_dt2_dy2 = (WAVE_SPEED * WAVE_SPEED * dt * dt) / (dy * dy);
    
    while (control[0] == 1 && control[1] < max_iterations) {
        for (int idx = tid; idx < total_points; idx += stride) {
            const int i = idx % nx;
            const int j = idx / nx;
            
            if (i == 0 || i == nx - 1 || j == 0 || j == ny - 1) {
                u_current[idx] = 0.0f;
                continue;
            }
            
            float laplacian_x = u_previous[idx + 1] - 2.0f * u_previous[idx] + u_previous[idx - 1];
            float laplacian_y = u_previous[idx + nx] - 2.0f * u_previous[idx] + u_previous[idx - nx];
            
            u_current[idx] = 2.0f * u_previous[idx] - u_two_ago[idx]
                           + c2_dt2_dx2 * laplacian_x
                           + c2_dt2_dy2 * laplacian_y;
            u_current[idx] *= DAMPING;
        }
        
        // Grid-wide synchronization
        grid.sync();
        
        if (tid == 0) {
            float* temp = u_two_ago;
            u_two_ago = u_previous;
            u_previous = u_current;
            u_current = temp;
            atomicAdd(&control[1], 1);
        }
        
        grid.sync();
    }
}

// Helper kernel to initialize wave field with a Gaussian pulse
extern "C" __global__ void initialize_gaussian_pulse_2d(
    float* field,
    const int nx,
    const int ny,
    const float center_x,
    const float center_y,
    const float sigma,
    const float amplitude)
{
    const int tid = blockIdx.x * blockDim.x + threadIdx.x;
    const int stride = blockDim.x * gridDim.x;
    const int total_points = nx * ny;
    
    for (int idx = tid; idx < total_points; idx += stride) {
        const int i = idx % nx;
        const int j = idx / nx;
        
        const float x = (float)i - center_x;
        const float y = (float)j - center_y;
        const float r2 = x * x + y * y;
        
        field[idx] = amplitude * expf(-r2 / (2.0f * sigma * sigma));
    }
}

// Helper kernel to add source term
extern "C" __global__ void add_source_term(
    float* field,
    const int source_idx,
    const float amplitude,
    const float frequency,
    const float time)
{
    if (threadIdx.x == 0 && blockIdx.x == 0) {
        field[source_idx] += amplitude * sinf(2.0f * 3.14159265f * frequency * time);
    }
}