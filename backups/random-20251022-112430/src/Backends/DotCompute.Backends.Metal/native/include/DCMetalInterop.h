#ifndef DCMETAL_INTEROP_H
#define DCMETAL_INTEROP_H

#ifdef __cplusplus
extern "C" {
#endif

#include <stdint.h>
#include <stdbool.h>

// Forward declarations
typedef void* DCMetalDevice;
typedef void* DCMetalCommandQueue;
typedef void* DCMetalCommandBuffer;
typedef void* DCMetalBuffer;
typedef void* DCMetalLibrary;
typedef void* DCMetalFunction;
typedef void* DCMetalComputePipelineState;
typedef void* DCMetalCommandEncoder;
typedef void* DCMetalCompileOptions;
typedef void* DCMetalError;

// Enums
typedef enum {
    DCMetalStorageModeShared = 0,
    DCMetalStorageModePrivate = 1,
    DCMetalStorageModeManaged = 2,
    DCMetalStorageModeMemoryless = 3
} DCMetalStorageMode;

typedef enum {
    DCMetalDeviceLocationBuiltIn = 0,
    DCMetalDeviceLocationSlot = 1,
    DCMetalDeviceLocationExternal = 2,
    DCMetalDeviceLocationUnspecified = UINT32_MAX
} DCMetalDeviceLocation;

typedef enum {
    DCMetalCommandBufferStatusNotEnqueued = 0,
    DCMetalCommandBufferStatusEnqueued = 1,
    DCMetalCommandBufferStatusCommitted = 2,
    DCMetalCommandBufferStatusScheduled = 3,
    DCMetalCommandBufferStatusCompleted = 4,
    DCMetalCommandBufferStatusError = 5
} DCMetalCommandBufferStatus;

typedef enum {
    DCMetalLanguageVersion10 = 0x10000,
    DCMetalLanguageVersion11 = 0x10100,
    DCMetalLanguageVersion12 = 0x10200,
    DCMetalLanguageVersion20 = 0x20000,
    DCMetalLanguageVersion21 = 0x20100,
    DCMetalLanguageVersion22 = 0x20200,
    DCMetalLanguageVersion23 = 0x20300,
    DCMetalLanguageVersion24 = 0x20400,
    DCMetalLanguageVersion30 = 0x30000,
    DCMetalLanguageVersion31 = 0x30100
} DCMetalLanguageVersion;

// Structures
typedef struct {
    size_t width;
    size_t height;
    size_t depth;
} DCMetalSize;

typedef struct {
    const char* name;
    uint64_t registryID;
    DCMetalDeviceLocation location;
    uint64_t locationNumber;
    uint64_t maxThreadgroupSize;
    uint64_t maxThreadsPerThreadgroup;
    uint64_t maxBufferLength;
    uint64_t recommendedMaxWorkingSetSize;
    bool hasUnifiedMemory;
    bool isLowPower;
    bool isRemovable;
    const char* supportedFamilies;
} DCMetalDeviceInfo;

// Callbacks
typedef void (*DCMetalCommandBufferCompletionHandler)(DCMetalCommandBufferStatus status);

// System Detection
bool DCMetal_IsMetalSupported(void);

// Device Management
DCMetalDevice DCMetal_CreateSystemDefaultDevice(void);
DCMetalDevice DCMetal_CreateDeviceAtIndex(int index);
int DCMetal_GetDeviceCount(void);
void DCMetal_ReleaseDevice(DCMetalDevice device);
DCMetalDeviceInfo DCMetal_GetDeviceInfo(DCMetalDevice device);

// Command Queue
DCMetalCommandQueue DCMetal_CreateCommandQueue(DCMetalDevice device);
void DCMetal_ReleaseCommandQueue(DCMetalCommandQueue queue);

// Command Buffer
DCMetalCommandBuffer DCMetal_CreateCommandBuffer(DCMetalCommandQueue queue);
void DCMetal_CommitCommandBuffer(DCMetalCommandBuffer buffer);
void DCMetal_WaitUntilCompleted(DCMetalCommandBuffer buffer);
void DCMetal_ReleaseCommandBuffer(DCMetalCommandBuffer buffer);
void DCMetal_SetCommandBufferCompletionHandler(DCMetalCommandBuffer buffer, DCMetalCommandBufferCompletionHandler handler);

// Buffers
DCMetalBuffer DCMetal_CreateBuffer(DCMetalDevice device, size_t length, DCMetalStorageMode mode);
DCMetalBuffer DCMetal_CreateBufferWithBytes(DCMetalDevice device, const void* bytes, size_t length, DCMetalStorageMode mode);
void* DCMetal_GetBufferContents(DCMetalBuffer buffer);
size_t DCMetal_GetBufferLength(DCMetalBuffer buffer);
void DCMetal_ReleaseBuffer(DCMetalBuffer buffer);
void DCMetal_DidModifyRange(DCMetalBuffer buffer, int64_t offset, int64_t length);
void DCMetal_CopyBuffer(DCMetalBuffer source, int64_t sourceOffset, DCMetalBuffer destination, int64_t destOffset, int64_t size);

// Compilation
DCMetalCompileOptions DCMetal_CreateCompileOptions(void);
void DCMetal_SetCompileOptionsFastMath(DCMetalCompileOptions options, bool enable);
void DCMetal_SetCompileOptionsLanguageVersion(DCMetalCompileOptions options, DCMetalLanguageVersion version);
void DCMetal_ReleaseCompileOptions(DCMetalCompileOptions options);
DCMetalLibrary DCMetal_CompileLibrary(DCMetalDevice device, const char* source, DCMetalCompileOptions options, DCMetalError* error);
DCMetalLibrary DCMetal_CreateLibraryWithSource(DCMetalDevice device, const char* source);
void DCMetal_ReleaseLibrary(DCMetalLibrary library);
DCMetalFunction DCMetal_GetFunction(DCMetalLibrary library, const char* name);
void DCMetal_ReleaseFunction(DCMetalFunction function);

// Pipeline State
DCMetalComputePipelineState DCMetal_CreateComputePipelineState(DCMetalDevice device, DCMetalFunction function, DCMetalError* error);
void DCMetal_ReleasePipelineState(DCMetalComputePipelineState state);
int DCMetal_GetMaxTotalThreadsPerThreadgroup(DCMetalComputePipelineState state);
void DCMetal_GetThreadExecutionWidth(DCMetalComputePipelineState state, int* x, int* y, int* z);

// Compute Command Encoder
DCMetalCommandEncoder DCMetal_CreateComputeCommandEncoder(DCMetalCommandBuffer buffer);
void DCMetal_SetComputePipelineState(DCMetalCommandEncoder encoder, DCMetalComputePipelineState state);
void DCMetal_SetBuffer(DCMetalCommandEncoder encoder, DCMetalBuffer buffer, size_t offset, int index);
void DCMetal_SetBytes(DCMetalCommandEncoder encoder, const void* bytes, size_t length, int index);
void DCMetal_DispatchThreadgroups(DCMetalCommandEncoder encoder, DCMetalSize gridSize, DCMetalSize threadgroupSize);
void DCMetal_EndEncoding(DCMetalCommandEncoder encoder);
void DCMetal_ReleaseEncoder(DCMetalCommandEncoder encoder);

// Error Handling
const char* DCMetal_GetErrorLocalizedDescription(DCMetalError error);
void DCMetal_ReleaseError(DCMetalError error);

#ifdef __cplusplus
}
#endif

#endif // DCMETAL_INTEROP_H