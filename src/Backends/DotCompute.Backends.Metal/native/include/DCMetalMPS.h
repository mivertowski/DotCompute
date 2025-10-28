#ifndef DCMETAL_MPS_H
#define DCMETAL_MPS_H

#ifdef __cplusplus
extern "C" {
#endif

#include <stdint.h>
#include <stdbool.h>
#include "DCMetalInterop.h"

// MPS Data Types
typedef enum {
    DCMPSDataTypeFloat32 = 0x10000020,
    DCMPSDataTypeFloat16 = 0x10000010,
    DCMPSDataTypeInt32 = 0x20000020,
    DCMPSDataTypeInt16 = 0x20000010,
    DCMPSDataTypeInt8 = 0x20000008
} DCMPSDataType;

// MPS Capabilities
typedef struct {
    bool supportsBLAS;
    bool supportsCNN;
    bool supportsNeuralNetwork;
    const char* gpuFamily;
} DCMPSCapabilities;

// Forward declarations
typedef void* DCMPSMatrixDescriptor;

// Capability Detection
DCMPSCapabilities DCMetal_QueryMPSCapabilities(DCMetalDevice device);

// Matrix Descriptor Management
DCMPSMatrixDescriptor DCMetal_CreateMatrixDescriptor(int rows, int columns, DCMPSDataType dataType);
void DCMetal_ReleaseMatrixDescriptor(DCMPSMatrixDescriptor descriptor);

// BLAS Operations
bool DCMetal_MPSMatrixMultiply(
    DCMetalDevice device,
    const float* matrixA, int rowsA, int colsA, bool transposeA,
    const float* matrixB, int rowsB, int colsB, bool transposeB,
    float* matrixC, int rowsC, int colsC,
    float alpha, float beta);

bool DCMetal_MPSMatrixVectorMultiply(
    DCMetalDevice device,
    const float* matrix, int rows, int cols, bool transpose,
    const float* vector, int vectorLength,
    float* result, int resultLength,
    float alpha, float beta);

// CNN Operations
bool DCMetal_MPSConvolution2D(
    DCMetalDevice device,
    const float* input, int inputHeight, int inputWidth, int inputChannels,
    const float* kernel, int kernelHeight, int kernelWidth, int outputChannels,
    float* output, int outputHeight, int outputWidth,
    int strideY, int strideX,
    int paddingY, int paddingX);

bool DCMetal_MPSMaxPooling2D(
    DCMetalDevice device,
    const float* input, int inputHeight, int inputWidth, int channels,
    float* output, int outputHeight, int outputWidth,
    int poolSizeY, int poolSizeX,
    int strideY, int strideX);

// Neural Network Operations
bool DCMetal_MPSNeuronReLU(DCMetalDevice device, const float* input, float* output, int count);
bool DCMetal_MPSNeuronSigmoid(DCMetalDevice device, const float* input, float* output, int count);
bool DCMetal_MPSNeuronTanh(DCMetalDevice device, const float* input, float* output, int count);

bool DCMetal_MPSBatchNormalization(
    DCMetalDevice device,
    const float* input,
    const float* gamma,
    const float* beta,
    const float* mean,
    const float* variance,
    float* output,
    int count,
    int channels,
    float epsilon);

#ifdef __cplusplus
}
#endif

#endif // DCMETAL_MPS_H
